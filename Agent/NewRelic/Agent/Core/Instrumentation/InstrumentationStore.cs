using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.Instrumentation
{
	public interface IInstrumentationStore
	{
		bool IsEmpty { get; }
		void AddOrUpdateInstrumentation(string name, string xml);
		void AddOrUpdateInstrumentation(InstrumentationSet instrumentationSet);
		KeyValuePair<string, string>[] GetInstrumentation();
		bool Clear();
	}

	public class InstrumentationStore : IInstrumentationStore
	{
		private readonly ConcurrentDictionary<string, string> _instrumentation = new ConcurrentDictionary<string, string>();

		public bool IsEmpty => _instrumentation.Count == 0;

		public void AddOrUpdateInstrumentation(InstrumentationSet instrumentationSet)
		{
			if (instrumentationSet == null || instrumentationSet.InstrumentationPoints.Count == 0)
			{
				return;
			}
			
			var xml = GenerateXml(instrumentationSet);
			AddOrUpdateInstrumentation(instrumentationSet.Name, xml);
		}

		public void AddOrUpdateInstrumentation(string name, string xml)
		{
			if (string.IsNullOrEmpty(name))
			{
				Log.Warn($"Instrumentation {nameof(name)} was null or empty.");
				return;
			}

			_instrumentation[name] = xml;
		}

		public KeyValuePair<string, string>[] GetInstrumentation()
		{
			// The following must use ToArray because ToArray is thread safe on a ConcurrentDictionary.
			return _instrumentation.ToArray();
		}

		public bool Clear()
		{
			var instrumentationCleared = false;
			foreach (var key in _instrumentation.Keys)
			{
				_instrumentation[key] = string.Empty;
				instrumentationCleared = true;
			}

			return instrumentationCleared;
		}

		private string GenerateXml(InstrumentationSet instrumentationSet)
		{
			var sb = new StringBuilder();
			var invalidInstrumentationPoints = new List<InstrumentationPoint>();
			foreach (var instrumentationPoint in instrumentationSet.InstrumentationPoints)
			{
				
				var declaringType = instrumentationPoint.MethodInfo.DeclaringType;
				if (declaringType != null)
				{
					var assemblyName = declaringType.Assembly.GetName().Name;
					var typeName = $"{declaringType.Namespace}.{declaringType.Name}";
					var methodName = instrumentationPoint.MethodInfo.Name;
					var tracerFactory = instrumentationPoint.TracerFactory;
					sb.AppendLine(CreateNewTracerFactoryNode(assemblyName, typeName, methodName, tracerFactory));
				}
				else
				{
					invalidInstrumentationPoints.Add(instrumentationPoint);
				}
			}

			if (invalidInstrumentationPoints.Count > 0)
			{
				Log.WarnFormat("Unexpected instrumentation points without DeclaringType: {0}", string.Join(", ", invalidInstrumentationPoints));
			}

			var instrumentationPointXml = sb.ToString();
			return !string.IsNullOrEmpty(instrumentationPointXml) ? string.Format(EmptyInstrumentationFormat, instrumentationPointXml) : string.Empty;
		}

		private const string EmptyInstrumentationFormat = @"<?xml version=""1.0"" encoding=""utf-8""?>
			<extension xmlns=""urn:newrelic-extension"">
				<instrumentation>
				{0}
				</instrumentation>
			</extension>";

		private string CreateNewTracerFactoryNode(string assemblyName, string typeName, string methodName, string tracerFactory)
		{
			var xml = string.IsNullOrEmpty(tracerFactory) ?

				$"<tracerFactory>" +
				$"  <match assemblyName=\"{assemblyName}\" className=\"{typeName}\">" +
				$"    <exactMethodMatcher methodName=\"{methodName}\" />" +
				$"  </match>" +
				$"</tracerFactory>" :

				$"<tracerFactory name=\"{tracerFactory}\">" +
				$"  <match assemblyName=\"{assemblyName}\" className=\"{typeName}\">" +
				$"    <exactMethodMatcher methodName=\"{methodName}\" />" +
				$"  </match>" +
				$"</tracerFactory>";

			return xml;
		}
	}
}
