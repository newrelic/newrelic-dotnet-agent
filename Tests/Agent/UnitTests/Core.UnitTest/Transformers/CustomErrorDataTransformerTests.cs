using System.Collections.Generic;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.WireModels;
using NUnit.Framework;
using Telerik.JustMock;
using Attribute = NewRelic.Agent.Core.Attributes.Attribute;

namespace NewRelic.Agent.Core.Transformers
{
	[TestFixture]
	public class CustomErrorDataTransformerTests
	{
		private CustomErrorDataTransformer _customErrorDataTransformer;

		private IConfiguration _configuration;

		private IAttributeService _attributeService;

		private IErrorTraceMaker _errorTraceMaker;

		private IErrorTraceAggregator _errorTraceAggregator;

		private IErrorEventMaker _errorEventMaker;

		private IErrorEventAggregator _errorEventAggregator;

		[SetUp]
		public void SetUp()
		{
			var configurationService = Mock.Create<IConfigurationService>();
			_configuration = Mock.Create<IConfiguration>();
			Mock.Arrange(() => _configuration.ErrorCollectorEnabled).Returns(true);
			Mock.Arrange(() => _configuration.CaptureCustomParameters).Returns(true);
			Mock.Arrange(() => configurationService.Configuration).Returns(_configuration);

			_attributeService = Mock.Create<IAttributeService>();
			_errorTraceMaker = Mock.Create<IErrorTraceMaker>();
			_errorTraceAggregator = Mock.Create<IErrorTraceAggregator>();
			_errorEventMaker = Mock.Create<IErrorEventMaker>();
			_errorEventAggregator = Mock.Create<IErrorEventAggregator>();

			_customErrorDataTransformer = new CustomErrorDataTransformer(configurationService, _attributeService, _errorTraceMaker, _errorTraceAggregator, _errorEventMaker, _errorEventAggregator);
		}

		[Test]
		public void Transform_SendsErrorTraceToAggregator()
		{
			var expectedErrorTrace = Mock.Create<ErrorTraceWireModel>();
			Mock.Arrange(() => _errorTraceMaker.GetErrorTrace(Arg.IsAny<AttributeCollection>(), Arg.IsAny<ErrorData>()))
				.Returns(expectedErrorTrace);

			float priority = 0.5f;
			_customErrorDataTransformer.Transform<object>(new ErrorData(), null, priority);

			Mock.Assert(() => _errorTraceAggregator.Collect(expectedErrorTrace));
		}

		[Test]
		public void Transform_SendsErrorEventToAggregator()
		{
			var expectedErrorEvent = Mock.Create<ErrorEventWireModel>();
			float priority = 0.5f;
			Mock.Arrange(() => _errorEventMaker.GetErrorEvent(Arg.IsAny<ErrorData>(), Arg.IsAny<AttributeCollection>(), priority))
				.Returns(expectedErrorEvent);

			_customErrorDataTransformer.Transform<object>(new ErrorData(), null, priority);

			Mock.Assert(() => _errorEventAggregator.Collect(expectedErrorEvent));
		}

		[Test]
		public void Transform_FiltersAttributesBeforeSendingThemToErrorTraceMaker()
		{
			// ARRANGE

			// Capture the attributes that are passed to IAttributeService.FilterAttributes, and mock the return value to be a single attribute, "key2".
			var expectedFilteredAttributes = new AttributeCollection();
			var attributesPassedToAttributeService = null as AttributeCollection;
			expectedFilteredAttributes.Add(Attribute.BuildCustomAttribute("key2", "value2"));
			Mock.Arrange(() => _attributeService.FilterAttributes(Arg.IsAny<AttributeCollection>(), AttributeDestinations.ErrorTrace))
				.Returns<AttributeCollection, AttributeDestinations>((attributes, _) =>
				{
					attributesPassedToAttributeService = attributes;
					return expectedFilteredAttributes;
				});

			// Capture the attributes that are passed to IErrorTraceMaker.GetErrorTrace
			var attributesPassedToErrorTraceMaker = null as AttributeCollection;
			Mock.Arrange(() => _errorTraceMaker.GetErrorTrace(Arg.IsAny<AttributeCollection>(), Arg.IsAny<ErrorData>()))
				.DoInstead<AttributeCollection, ErrorData>((attributes, _) => attributesPassedToErrorTraceMaker = attributes);

			// ACT
			var inputAttributes = new Dictionary<string, string>
			{
				{"key1", "value1"},
				{"key2", "value2"}
			};
			float priority = 0.5f;
			_customErrorDataTransformer.Transform(new ErrorData(), inputAttributes, priority);

			// ASSERT

			// Verify attributes passed to IAttributeService.FilterAttributes
			Assert.AreEqual("value1", attributesPassedToAttributeService.GetUserAttributesDictionary()["key1"]);
			Assert.AreEqual("value2", attributesPassedToAttributeService.GetUserAttributesDictionary()["key2"]);
			Assert.AreEqual(2, attributesPassedToAttributeService.Count());

			// Verify attributes passed to IErrorTraceMaker.GetErrorTrace
			Assert.AreEqual("value2", attributesPassedToErrorTraceMaker.GetUserAttributesDictionary()["key2"]);
			Assert.AreEqual(1, attributesPassedToErrorTraceMaker.Count());
		}

		[Test]
		public void Transform_DoesNotSendErrorTraceToAggregator_IfDisabledViaConfig()
		{
			Mock.Arrange(() => _configuration.ErrorCollectorEnabled).Returns(false);

			var expectedErrorTrace = Mock.Create<ErrorTraceWireModel>();
			Mock.Arrange(() => _errorTraceMaker.GetErrorTrace(Arg.IsAny<AttributeCollection>(), Arg.IsAny<ErrorData>()))
				.Returns(expectedErrorTrace);

			float priority = 0.5f;
			_customErrorDataTransformer.Transform<object>(new ErrorData(), null, priority);

			Mock.Assert(() => _errorTraceAggregator.Collect(expectedErrorTrace), Occurs.Never());
		}
	}
}
