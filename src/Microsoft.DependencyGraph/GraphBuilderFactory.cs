using System.Collections.Generic;

namespace Microsoft.DependencyGraph
{
    public readonly record struct GraphReference<TKey, TEdge>(TKey Key, TEdge Edge);

    public abstract class GraphBuilderFactory<TKey, TNode, TEdge>
            where TKey : notnull
    {
        public abstract GraphNode<TNode> CreateNode(TKey key);

        public abstract IEnumerable<GraphReference<TKey, TEdge>> GetReferences(GraphNode<TNode> node);
    }
}