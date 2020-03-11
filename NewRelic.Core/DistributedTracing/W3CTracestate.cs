using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NewRelic.Core.DistributedTracing
{
	public class W3CTracestate
	{
		// VendorstateEntries:
		//    {
		//        { "rojo", "00f067aa0ba902b7" },
		//        { "congo", "t61rcWkgMzE" },
		//        { "abc", "ujv" },
		//        { "xyz", "mmm" },
		//    }
		// ingested order must be maintained for outgoing header

		private const string NRVendorString = "@nr";
		private const int NumberOfEntries = 9;
		private const int MaxDecimalPlaces = 6;
		private const int SupportedVersion = 0;

		public List<string> VendorstateEntries { get; set; }	// nonNR, nonTrusted: 55@dd=string, 45@nr=string

		// fields pulled from the trusted tracestate NR entry
		public string AccountKey { get; set; }  // "33" from "33@nr"
		public int Version { get; set; }
		public DistributedTracingParentType ParentType { get; set; }
		public string AccountId { get; set; }
		public string AppId { get; set; }
		public string SpanId { get; set; }
		public string TransactionId { get; set; }
		public int Sampled { get; set; }
		public float Priority { get; set; }
		public long Timestamp { get; set; }

		public W3CTracestate(List<string> vendorstates, string accountKey, int version, int parentType, string accountId, string appId, string spanId, string transactionId, int sampled, float priority, long timestamp)
		{
			VendorstateEntries = vendorstates;
			AccountKey = accountKey;
			Version = version;
			ParentType = (DistributedTracingParentType)parentType;
			AccountId = accountId;
			AppId = appId;
			SpanId = spanId;
			TransactionId = transactionId;
			Sampled = sampled;
			Priority = priority;
			Timestamp = timestamp;
		}

		public List<string> GetVendorNames()
		{
			// TODO: better string management, linq ?
			var names = new List<string>();

			// pull the vendor names from VendorstateEntries
			foreach (string entry in VendorstateEntries)
			{
				// parse out the vendor name and add to list
				names.Add(entry.Substring(0, entry.IndexOf('=')));
			}

			return names;
		}

		public KeyValuePair<string, string> ToHeaderFormat()
		{
			var sb = new StringBuilder();

			// TODO: deal with too long

			sb.Append(this.ToString());

			foreach (string vendorEntry in VendorstateEntries)
			{
				sb.Append($",{vendorEntry}");
			}

			return new KeyValuePair<string, string>("tracestate", sb.ToString());
		}

		public override string ToString() => $"{AccountId}@nr={Version}-{(int)ParentType}-{AccountId}-{AppId}-{SpanId}-{TransactionId}-{Sampled.ToString()}-{Priority}-{Timestamp}";

		public static W3CTracestate GetW3CTracestateFromHeaders(IList<string> tracestateCollection, string trustedAccountKey) 
		{
			var tracestateEntries = TryExtractTracestateHeaders(tracestateCollection);

			if (tracestateEntries.Count > 0)
			{
				var newRelicTraceStateEntry = tracestateEntries.Where(entry => entry.Key.Equals($"{trustedAccountKey}{NRVendorString}")).FirstOrDefault();

				if (!TracestateUtils.ValidateValue(newRelicTraceStateEntry.Value))
				{
					return null;
				}

				var splits = newRelicTraceStateEntry.Value.Split('-');

				if(splits.Count() != NumberOfEntries) 
				{
					return null;
				}

				var vendorstates = tracestateEntries.Where(entry => !entry.Key.Contains($"{trustedAccountKey}{NRVendorString}")).Select(entry => $"{entry.Key}={entry.Value}").ToList();

				//required field
				if (!int.TryParse(splits[0], out int version))
				{
					return null;
				}
				else if(version != SupportedVersion) 
				{
					return null;
				}

				//required field
				if(!int.TryParse(splits[1], out int parentType))
				{
					return null;
				}
				else if(parentType < (int)DistributedTracingParentType.App || parentType > (int)DistributedTracingParentType.Mobile) 
				{
					return null;
				}

				var accountId = splits[2];

				//required field
				if (string.IsNullOrEmpty(accountId)) 
				{
					return null;
				}

				var appId = splits[3];
				
				//required field
				if (string.IsNullOrEmpty(appId))
				{
					return null;
				}

				var spanId = splits[4];
				var transactionId = splits[5];

				int sampled = default;
				if(!string.IsNullOrEmpty(splits[6]))
				{
					if (!int.TryParse(splits[6], out sampled))
					{
						return null;
					}
					else if (sampled != (int)SampledEnum.IsTrue && sampled != (int)SampledEnum.IsFalse)
					{
						return null;
					}
				}

				float priority = default; 
				if(!string.IsNullOrEmpty(splits[6]) && !TryParseAndValidatePriority(splits[7], out priority)) 
				{
					return null;
				}

				//required field
				if (!long.TryParse(splits[8], out long timestamp))
				{
					return null;
				}

				return new W3CTracestate(vendorstates, trustedAccountKey, version, parentType,
					accountId, appId, spanId, transactionId, sampled, priority, timestamp);
			}

			return null;
		}

		private static List<KeyValuePair<string, string>> TryExtractTracestateHeaders(IList<string> tracestateCollection)
		{
			List<KeyValuePair<string, string>> tracestateEntries = new List<KeyValuePair<string, string>>();

			if (tracestateCollection != null)
			{
				// Iterate in reverse order.
				for (var i = tracestateCollection.Count - 1; i >= 0; i--)
				{
					if (!TracestateUtils.ParseTracestate(tracestateCollection[i], tracestateEntries))
					{
						break;
					}
				}
			}

			return tracestateEntries;
		}

		private static bool TryParseAndValidatePriority(string priorityString, out float priority)
		{
			priority = default;
			var lastIndex = priorityString.Length - 1;

			//Checking if priority value is rounded to 6 decimal places
			if (priorityString.IndexOf('.') > -1)
			{
				if (lastIndex - priorityString.IndexOf('.') > MaxDecimalPlaces)
				{
					return false;
				}

				if (priorityString[lastIndex] == '0')
				{
					return false;
				}
			}

			if (float.TryParse(priorityString, out priority))
			{
				return true;
			}

			return false;
		}
	}

	enum SampledEnum 
	{
		IsFalse,
		IsTrue
	}
}
