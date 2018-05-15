using JetBrains.Annotations;
using System.Collections;

namespace NewRelic.Agent.Core.ThreadProfiling
{
	public interface IThreadProfilingProcessing
	{
		[NotNull]
		ArrayList PruningList { get; }
		void AddNodeToPruningList(ProfileNode node);
		void ResetCache();
		void SortPruningTree();
	}
}
