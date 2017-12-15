using System.Collections;
using JetBrains.Annotations;

namespace NewRelic.Agent.Core.ThreadProfiling
{
	public interface IThreadProfilingProcessing
	{
		[NotNull]
		ArrayList PruningList { get; }
		void UpdateTree(StackInfo stackInfo, uint depth);
		void AddNodeToPruningList(ProfileNode node);
		void ResetCache();
		void SortPruningTree();
	}
}
