using System;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Aggregators
{
	public struct EventHarvestData
	{
		[JsonProperty("reservoir_size")]
		public uint ReservoirSize { get; private set; }
		[JsonProperty("events_seen")]
		public uint EventsSeen { get; private set; }

		public EventHarvestData(uint reservoirSize, uint eventsSeen)
		{
			ReservoirSize = reservoirSize;
			EventsSeen = eventsSeen;
		}
	}
}