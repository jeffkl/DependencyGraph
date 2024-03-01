using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DependencyGraph
{
    internal sealed class ParallelWorkSet<TKey, TResult> : IDisposable
    {
        private readonly CancellationToken _cancellationToken;

        private readonly IEqualityComparer<TKey> _comparer;

        private readonly List<Exception> _exceptions = new(0);

        private readonly ConcurrentDictionary<TKey, Lazy<TResult>> _inProgressOrCompletedWork;

        private readonly int _maxDegreeOfParallelism;

        private readonly ConcurrentQueue<Lazy<TResult>> _queue = new();

        private readonly SemaphoreSlim _semaphore = new(0, int.MaxValue);

        private readonly Task[] _workerTasks;

        private bool _isSchedulingCompleted;

        private long _pendingCount;

        public ParallelWorkSet(int maxDegreeOfParallelism, CancellationToken cancellationToken)
            : this(maxDegreeOfParallelism, EqualityComparer<TKey>.Default, cancellationToken)
        {
        }

        public ParallelWorkSet(int maxDegreeOfParallelism, IEqualityComparer<TKey> comparer, CancellationToken cancellationToken)
        {
            if (maxDegreeOfParallelism < 0)
            {
                throw new ArgumentException("Degree of parallelism must be a positive integer.", nameof(maxDegreeOfParallelism));
            }

            _maxDegreeOfParallelism = maxDegreeOfParallelism;
            _comparer = comparer;
            _cancellationToken = cancellationToken;

            _inProgressOrCompletedWork = new ConcurrentDictionary<TKey, Lazy<TResult>>(_comparer);

            _workerTasks = new Task[_maxDegreeOfParallelism];

            for (int i = 0; i < maxDegreeOfParallelism; i++)
            {
                _workerTasks[i] = CreateWorkerTask();
            }
        }

        public bool IsCompleted
        {
            get => Volatile.Read(ref _isSchedulingCompleted);
            private set => Volatile.Write(ref _isSchedulingCompleted, value);
        }

        /// <summary>
        /// Enqueues a work item to the work set.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="workFunc"></param>
        public void AddWork(TKey key, Func<TResult> workFunc)
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (IsCompleted)
            {
                throw new InvalidOperationException("Cannot add new work after work set is marked as completed.");
            }

            Lazy<TResult> workItem = new(workFunc);

            if (!_inProgressOrCompletedWork.TryAdd(key, workItem))
            {
                return;
            }

            Interlocked.Increment(ref _pendingCount);

            _queue.Enqueue(workItem);

            _semaphore.Release();
        }

        public async Task<IReadOnlyDictionary<TKey, TResult>> CompleteAsync()
        {
            if (!IsCompleted)
            {
                while (!_cancellationToken.IsCancellationRequested && Interlocked.Read(ref _pendingCount) > 0)
                {
                    ExecuteWorkItem();
                }
            }

            IsCompleted = true;

            _semaphore.Release();

            await Task.WhenAll(_workerTasks);

            Dictionary<TKey, TResult> completedWork = new(_inProgressOrCompletedWork.Count, _comparer);

            foreach (KeyValuePair<TKey, Lazy<TResult>> item in _inProgressOrCompletedWork)
            {
                Lazy<TResult> lazy = item.Value;

                if (lazy.IsValueCreated)
                {
                    completedWork[item.Key] = lazy.Value;
                }
            }

            if (_exceptions.Count > 0)
            {
                throw new AggregateException(_exceptions);
            }

            return new ReadOnlyDictionary<TKey, TResult>(completedWork);
        }

        public void Dispose()
        {
            _semaphore.Dispose();
        }

        private Task CreateWorkerTask()
        {
            return Task.Run(
                async () =>
                {
                    bool shouldStopAllWorkers = false;

                    while (!shouldStopAllWorkers)
                    {
                        try
                        {
                            await _semaphore.WaitAsync(_cancellationToken);
                        }
                        catch (TaskCanceledException)
                        {
                            shouldStopAllWorkers = true;
                        }

                        try
                        {
                            ExecuteWorkItem();
                        }
                        finally
                        {
                            shouldStopAllWorkers = !shouldStopAllWorkers ? Interlocked.Read(ref _pendingCount) == 0 && IsCompleted : shouldStopAllWorkers;

                            if (shouldStopAllWorkers)
                            {
                                _semaphore.Release(_maxDegreeOfParallelism);
                            }
                        }
                    }
                },
                _cancellationToken);
        }

        private void ExecuteWorkItem()
        {
            if (!_cancellationToken.IsCancellationRequested && _queue.TryDequeue(out Lazy<TResult> workItem))
            {
                try
                {
                    _ = workItem.Value;
                }
                catch (Exception ex)
                {
                    lock (_exceptions)
                    {
                        _exceptions.Add(ex);
                    }
                }
                finally
                {
                    Interlocked.Decrement(ref _pendingCount);
                }
            }
        }
    }
}