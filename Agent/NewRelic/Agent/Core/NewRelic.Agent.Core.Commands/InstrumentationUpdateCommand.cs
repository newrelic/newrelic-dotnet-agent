using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.ThreadProfiling;
using Newtonsoft.Json.Linq;

namespace NewRelic.Agent.Core.Commands
{
	public class InstrumentationUpdateCommand : AbstractCommand
	{
		[NotNull]
		public INativeMethods NativeMethods { get; }

		public InstrumentationUpdateCommand([NotNull] INativeMethods nativeMethods)
		{
			Name = "instrumentation_update";
			NativeMethods = nativeMethods;
		}

		public override Object Process(IDictionary<String, Object> arguments)
		{
			var errorMessage = InstrumentationUpdate(arguments);
			if (errorMessage == null)
				return new Dictionary<String, Object>();

			Log.Error(errorMessage);

			// Other commands send errors under the error key, but I've verified the UI uses `errors`
			// Originally noticed this in the java agent code: https://source.datanerd.us/java-agent/java_agent/blob/master/newrelic-agent/src/main/java/com/newrelic/agent/reinstrument/ReinstrumentResult.java
			return new Dictionary<String, Object>
			{
				{"errors", errorMessage}
			};
		}

		private string InstrumentationUpdate(IDictionary<string, object> arguments)
		{
			if (arguments.TryGetValue("instrumentation", out object instrumentationSetObject))
			{
				var instrumentationSet = instrumentationSetObject as JObject;
				if (instrumentationSet != null)
				{
					if (instrumentationSet.TryGetValue("config", out JToken xml)) {
						Log.Info("Applying instrumentation");

						NativeMethods.AddCustomInstrumentation("inst", xml.ToString());
						NativeMethods.ApplyCustomInstrumentation();
					} else
					{
						return "The instrumentation config key was missing";
					}
				}
				else
				{
					return "The instrumentation update instrumentation set was empty";
				}
			} else
			{
				return "The instrumentation key was missing";
			}
			return null;
		}
	}
}