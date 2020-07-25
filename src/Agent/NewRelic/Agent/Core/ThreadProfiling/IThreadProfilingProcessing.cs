using System.Collections;

namespace NewRelic.Agent.Core.ThreadProfiling
{
    public interface IThreadProfilingProcessing
    {
        ArrayList PruningList { get; }
        void UpdateTree(StackInfo stackInfo, uint depth);
        void AddNodeToPruningList(ProfileNode node);
        void ResetCache();
        void SortPruningTree();
    }
}
