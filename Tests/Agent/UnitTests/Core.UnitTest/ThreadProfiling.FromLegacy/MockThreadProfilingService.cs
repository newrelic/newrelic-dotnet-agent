using System.Collections;

namespace NewRelic.Agent.Core.ThreadProfiling
{
	public class MockThreadProfilingService : IThreadProfilingSessionControl, IThreadProfilingProcessing
	{
		public int ProfileSessionId { get; private set; }

		public uint Frequency { get; private set; }
		public uint Duration { get; private set; }

		public int ProfileId { get; private set;}
		public bool ReportData { get; private set;}

		public bool ProfileSessionIsActive { get; private set; }

		public bool StartThreadProfilingSession(
			int profileSessionId,
			uint frequencyInMsec,
			uint durationInMsec)
		{
			ProfileSessionId = profileSessionId;
			Frequency = frequencyInMsec;
			Duration = durationInMsec;
			ProfileSessionIsActive = true;
			return true;
		}

		public bool StopThreadProfilingSession(int profileId, bool reportData)
		{
			bool result = true;

			ProfileId = profileId;
			ReportData = reportData;

			if (ProfileSessionIsActive)
			{
				ProfileSessionIsActive = false;
				result = false;
			}
			return result;
		}

		public void AddNodeToPruningList(ProfileNode node)
		{
		}

		public void ResetCache()
		{
		}

		public void SortPruningTree()
		{
		}

		public ArrayList PruningList { get; set; }
	}
}
