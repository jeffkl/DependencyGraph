using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.DependencyGraph
{
    [DebuggerDisplay("{Item, nq}, #in={ReferencingNodes.Count}, #out={References.Count}")]
    public struct GraphNode<TNode>
    {
        private readonly HashSet<GraphNode<TNode>> _references = new();
        private readonly HashSet<GraphNode<TNode>> _parents = new();

        public GraphNode(TNode item)
        {
            Item = item;
        }

        public TNode Item { get; }

        public string FullPath { get; init; }

        public IReadOnlyCollection<GraphNode<TNode>> References => _references;

        public IReadOnlyCollection<GraphNode<TNode>> ReferencingNodes => _parents;

        internal void AddReference<TEdge>(GraphNode<TNode> reference, TEdge edge, GraphEdges<TNode, TEdge> edges)
        {
            _references.Add(reference);

            reference._parents.Add(this);

            edges.AddOrUpdateEdge((this, reference), edge);
        }
    }
}