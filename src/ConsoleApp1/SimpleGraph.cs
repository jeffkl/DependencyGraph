using Microsoft.DependencyGraph;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    [DebuggerDisplay("{Name, nq}")]
    public sealed class Foo
    {
        public string Name { get; set; }
    }

    internal class SimpleGraph
    {
        public static readonly Dictionary<string, string> _rawGraph = new()
        {
            ["A"] = "B;C;D;E",
            ["B"] = "D",
            ["C"] = "D;E",
            ["D"] = "E",
            ["E"] = string.Empty
        };

        private readonly GraphBuilder<string, Foo, string>.DependencyGraph _dependencyGraph;

        private SimpleGraph(GraphBuilder<string, Foo, string>.DependencyGraph dependencyGraph)
        {
            _dependencyGraph = dependencyGraph;
        }

        public IReadOnlyCollection<GraphNode<Foo>> EntryPointNodes => _dependencyGraph.EntryPointNodes;

        public IReadOnlyCollection<GraphNode<Foo>> Nodes => _dependencyGraph.Nodes;

        public IReadOnlyCollection<GraphNode<Foo>> RootNodes => _dependencyGraph.RootNodes;

        public IReadOnlyCollection<GraphNode<Foo>> NodesTopologicallySorted => _dependencyGraph.NodesTopologicallySortedLazy.Value;

        public static async Task<SimpleGraph> CreateAsync(string[] entryProjects, Dictionary<string, string> rawGraph)
        {
            SimpleGraphFactory graphFactory = new(rawGraph);

            GraphBuilder<string, Foo, string> graphBuilder = new(entryProjects, StringComparer.OrdinalIgnoreCase, graphFactory, Environment.ProcessorCount, CancellationToken.None);

            GraphBuilder<string, Foo, string>.DependencyGraph dependencyGraph = await graphBuilder.BuildAsync();

            return new SimpleGraph(dependencyGraph);
        }

        private class SimpleGraphFactory : GraphBuilderFactory<string, Foo, string>
        {
            private readonly Dictionary<string, string> _graph;

            public SimpleGraphFactory(Dictionary<string, string> graph)
            {
                _graph = graph;
            }

            public override GraphNode<Foo> CreateNode(string key)
            {
                Foo foo = new Foo
                {
                    Name = key,
                };

                return new GraphNode<Foo>(foo)
                {
                    FullPath = key
                };
            }

            public override IEnumerable<GraphReference<string, string>> GetReferences(GraphNode<Foo> node)
            {
                string[] references = _graph.TryGetValue(node.Item.Name, out string value) && !string.IsNullOrWhiteSpace(value)
                    ? value.Split(';')
                    : Array.Empty<string>();

                foreach (string reference in references)
                {
                    yield return new GraphReference<string, string>(reference, reference);
                }
            }
        }
    }
}