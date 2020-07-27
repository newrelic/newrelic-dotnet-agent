using System.Collections.Generic;
using JetBrains.Annotations;

namespace NewRelic.Trie
{
    public class TrieNode<T>
    {
        [NotNull]
        public readonly T Data;

        [NotNull]
        public readonly ICollection<TrieNode<T>> Children = new List<TrieNode<T>>();

        public TrieNode([NotNull] T metaData)
        {
            Data = metaData;
        }
    }
}
