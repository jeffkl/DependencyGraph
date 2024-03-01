using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DependencyGraph
{
    public sealed class GraphBuilder<TKey, TNode, TEdge>
        where TKey : notnull
    {
        private const int ImplicitWorkerCount = 1;

        private readonly GraphEdges<TNode, TEdge> _edges;
        private readonly ImmutableArray<TKey> _entryPoints;
        private readonly GraphBuilderFactory<TKey, TNode, TEdge> _feeder;
        private readonly ParallelWorkSet<TKey, (TKey, GraphNode<TNode>, List<GraphReference<TKey, TEdge>>)> _graphWorkSet;
        private readonly IEqualityComparer<TKey> _keyComparer;

        public GraphBuilder(
            IEnumerable<TKey> entryPoints,
            IEqualityComparer<TKey> comparer,
            GraphBuilderFactory<TKey, TNode, TEdge> feeder,
            int degreeOfParallelism,
            CancellationToken cancellationToken)
        {
            _entryPoints = entryPoints.ToImmutableArray();
            _feeder = feeder;
            _keyComparer = comparer;

            _graphWorkSet = new ParallelWorkSet<TKey, (TKey, GraphNode<TNode>, List<GraphReference<TKey, TEdge>>)>(
                degreeOfParallelism - ImplicitWorkerCount,
                _keyComparer,
                cancellationToken);

            _edges = new GraphEdges<TNode, TEdge>();
        }

        private enum NodeVisitationState
        {
            // the project has been evaluated and its project references are being processed
            InProcess,

            // all project references of this project have been processed
            Processed
        }

        public async Task<DependencyGraph> BuildAsync()
        {
            if (_graphWorkSet.IsCompleted)
            {
                return default;
            }

            foreach (TKey entryPoint in _entryPoints)
            {
                SubmitWork(entryPoint);
            }

            IReadOnlyDictionary<TKey, (TKey, GraphNode<TNode> Node, List<GraphReference<TKey, TEdge>>)> allNodes = await _graphWorkSet.CompleteAsync();

            foreach (KeyValuePair<TKey, (TKey _, GraphNode<TNode> Node, List<GraphReference<TKey, TEdge>> Edges)> item in allNodes)
            {
                GraphNode<TNode> currentNode = item.Value.Node;

                foreach (GraphReference<TKey, TEdge> reference in item.Value.Edges)
                {
                    currentNode.AddReference(allNodes[reference.Key].Node, reference.Edge, _edges);
                }
            }

            IReadOnlyCollection<GraphNode<TNode>> entryPointNodes = _entryPoints.Select(i => allNodes[i].Node).ToImmutableList();

            DetectCycles(entryPointNodes);

            IReadOnlyCollection<GraphNode<TNode>> rootNodes = GetGraphRoots(entryPointNodes);

            IReadOnlyCollection<GraphNode<TNode>> nodes = allNodes.Values.Select(i => i.Node).ToImmutableList();

            Lazy<IReadOnlyCollection<GraphNode<TNode>>> nodesTopologicallySortedLazy = new(() => TopologicalSort(rootNodes, nodes));

            return new DependencyGraph(entryPointNodes, rootNodes, nodes, _edges, nodesTopologicallySortedLazy);
        }

        public readonly record struct DependencyGraph(IReadOnlyCollection<GraphNode<TNode>> EntryPointNodes, IReadOnlyCollection<GraphNode<TNode>> RootNodes, IReadOnlyCollection<GraphNode<TNode>> Nodes, GraphEdges<TNode, TEdge> Edges, Lazy<IReadOnlyCollection<GraphNode<TNode>>> NodesTopologicallySortedLazy);

        private static IReadOnlyCollection<GraphNode<TNode>> TopologicalSort(IReadOnlyCollection<GraphNode<TNode>> graphRoots, IReadOnlyCollection<GraphNode<TNode>> graphNodes)
        {
            List<GraphNode<TNode>> result = new List<GraphNode<TNode>>(graphNodes.Count);
            Queue<GraphNode<TNode>> partialRoots = new Queue<GraphNode<TNode>>(graphNodes.Count);
            Dictionary<GraphNode<TNode>, int> inDegree = graphNodes.ToDictionary(n => n, n => n.ReferencingNodes.Count);

            foreach (GraphNode<TNode> root in graphRoots)
            {
                partialRoots.Enqueue(root);
            }

            while (partialRoots.Count != 0)
            {
                GraphNode<TNode> partialRoot = partialRoots.Dequeue();

                result.Add(partialRoot);

                foreach (GraphNode<TNode> reference in partialRoot.References)
                {
                    if (--inDegree[reference] == 0)
                    {
                        partialRoots.Enqueue(reference);
                    }
                }
            }

            //ErrorUtilities.VerifyThrow(toposort.Count == graphNodes.Count, "sorted node count must be equal to total node count");

            result.Reverse();

            return result;
        }

        private static IReadOnlyCollection<GraphNode<TNode>> GetGraphRoots(IReadOnlyCollection<GraphNode<TNode>> entryPointNodes)
        {
            List<GraphNode<TNode>> graphRoots = new(entryPointNodes.Count);

            foreach (GraphNode<TNode> entryPointNode in entryPointNodes)
            {
                if (entryPointNode.ReferencingNodes.Count == 0)
                {
                    graphRoots.Add(entryPointNode);
                }
            }

            graphRoots.TrimExcess();

            return graphRoots;
        }

        private void DetectCycles(
            IReadOnlyCollection<GraphNode<TNode>> entryPointNodes)
        {
            Dictionary<GraphNode<TNode>, NodeVisitationState> nodeStates = new();

            foreach (GraphNode<TNode> entryPointNode in entryPointNodes)
            {
                if (!nodeStates.TryGetValue(entryPointNode, out NodeVisitationState state))
                {
                    VisitNode(entryPointNode, nodeStates);
                }
                else
                {
                    //ErrorUtilities.VerifyThrow(
                    //    state == NodeVisitationState.Processed,
                    //    "entrypoints should get processed after a call to detect cycles");
                }
            }

            return;

            (bool success, List<GraphNode<TNode>> nodesInCycle) VisitNode(GraphNode<TNode> node, IDictionary<GraphNode<TNode>, NodeVisitationState> nodeState)
            {
                nodeState[node] = NodeVisitationState.InProcess;

                foreach (GraphNode<TNode> referenceNode in node.References)
                {
                    if (nodeState.TryGetValue(referenceNode, out NodeVisitationState projectReferenceNodeState))
                    {
                        if (projectReferenceNodeState == NodeVisitationState.InProcess)
                        {
                            List<GraphNode<TNode>> nodesInCycle = new() { referenceNode, referenceNode };

                            if (node.Equals(referenceNode))
                            {
                                // the project being evaluated has a reference to itself
                                throw new Exception($"There is a circular dependency involving the following nodes: {FormatCircularDependencyError(nodesInCycle)}");
                            }

                            return (false, nodesInCycle);
                        }
                    }
                    else
                    {
                        (bool success, List<GraphNode<TNode>> nodesInCycle) loadReference = VisitNode(referenceNode, nodeState);
                        if (!loadReference.success)
                        {
                            if (loadReference.nodesInCycle[0].Equals(node))
                            {
                                // we have reached the nth project in the cycle, form error message and throw
                                loadReference.nodesInCycle.Add(referenceNode);
                                loadReference.nodesInCycle.Add(node);

                                throw new Exception($"There is a circular dependency involving the following nodes: {FormatCircularDependencyError(loadReference.nodesInCycle)}");
                            }

                            // this is one of the projects in the circular dependency
                            // update the list of projects in cycle and return the list to the caller
                            loadReference.nodesInCycle.Add(referenceNode);
                            return (false, loadReference.nodesInCycle);
                        }
                    }
                }

                nodeState[node] = NodeVisitationState.Processed;

                return (true, null);
            }

            string FormatCircularDependencyError(List<GraphNode<TNode>> nodesInCycle)
            {
                StringBuilder errorMessage = new StringBuilder(nodesInCycle.Select(p => p.FullPath.Length).Sum());

                errorMessage.AppendLine();
                for (int i = nodesInCycle.Count - 1; i >= 0; i--)
                {
                    if (i != 0)
                    {
                        errorMessage.Append(nodesInCycle[i].FullPath)
                            .AppendLine(" ->");
                    }
                    else
                    {
                        errorMessage.Append(nodesInCycle[i].FullPath);
                    }
                }

                return errorMessage.ToString();
            }
        }

        private (TKey, GraphNode<TNode>, List<GraphReference<TKey, TEdge>>) GetNode(TKey key)
        {
            GraphNode<TNode> node = _feeder.CreateNode(key);

            List<GraphReference<TKey, TEdge>> references = new();

            foreach (GraphReference<TKey, TEdge> reference in _feeder.GetReferences(node))
            {
                SubmitWork(reference.Key);

                references.Add(reference);
            }

            return (key, node, references);
        }

        private void SubmitWork(TKey key)
        {
            _graphWorkSet.AddWork(key, () => GetNode(key));
        }
    }

    public sealed class GraphEdges<TNode, TEdge>
    {
        private readonly ConcurrentDictionary<(GraphNode<TNode>, GraphNode<TNode>), TEdge> _edges = new();

        internal int Count => _edges.Count;

        public TEdge this[(GraphNode<TNode> node, GraphNode<TNode> reference) key]
        {
            get
            {
                if (!_edges.TryGetValue(key, out TEdge edge))
                {
                    throw new InvalidOperationException();
                }

                return edge;
            }
        }

        public void AddOrUpdateEdge((GraphNode<TNode> node, GraphNode<TNode> reference) key, TEdge edge)
        {
            _edges.AddOrUpdate(
                key,
                addValueFactory: static ((GraphNode<TNode> node, GraphNode<TNode> reference) key, TEdge referenceItem) => referenceItem,
                updateValueFactory: static ((GraphNode<TNode> node, GraphNode<TNode> reference) key, TEdge existingItem, TEdge newItem) =>
                {
                    return newItem;
                },
                edge);
        }

        public void RemoveEdge((GraphNode<TNode> node, GraphNode<TNode> reference) key)
        {
            if (!_edges.TryRemove(key, out _))
            {
                throw new InvalidOperationException();
            }
        }

        internal bool HasEdge((GraphNode<TNode> node, GraphNode<TNode> reference) key) => _edges.ContainsKey(key);
    }
}