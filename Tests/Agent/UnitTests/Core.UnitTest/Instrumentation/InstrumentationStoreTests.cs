using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Instrumentation
{
	[TestFixture]
	public class InstrumentationStoreTests
	{
		[Test]
		public void AddingInstrumentationSetsToInstrumentationStoreGeneratesExpectedXml()
		{
			var currentMethod = MethodBase.GetCurrentMethod() as MethodInfo;
			var instrumentationStore = new InstrumentationStore();
			var instrumentationSet = new InstrumentationSet("SomeInstrumentation");
			var instrumentationPoint = new InstrumentationPoint(currentMethod);

			instrumentationSet.Add(instrumentationPoint);
			instrumentationStore.AddOrUpdateInstrumentation(instrumentationSet);

			var assemblyName = currentMethod.DeclaringType.Assembly.GetName().Name;
			var typeName = $"{currentMethod.DeclaringType.Namespace}.{currentMethod.DeclaringType.Name}";
			var methodName = currentMethod.Name;

			var expectedXml =
				$@"<?xml version=""1.0"" encoding=""utf-8""?>
				<extension xmlns=""urn:newrelic-extension"">
					<instrumentation>
						<tracerFactory>
							<match assemblyName=""{assemblyName}"" className=""{typeName}"">
								<exactMethodMatcher methodName=""{methodName}"" />
							</match>
						</tracerFactory>
					</instrumentation>
				</extension>";

			var expectedXdoc = XDocument.Parse(expectedXml);
			var actualXdoc = XDocument.Parse(instrumentationStore.GetInstrumentation().First().Value);

			Assert.IsTrue(XNode.DeepEquals(expectedXdoc, actualXdoc));
		}
	}
}
