using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Configuration;
using NewRelic.SystemExtensions;

namespace NewRelic.Agent
{
	public class LabelsService : ILabelsService
	{
		[NotNull]
		private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(typeof(LabelsService));

		private const Int32 MaxLabels = 64;
		private const Int32 MaxLength = 255;

		[NotNull]
		private readonly IConfigurationService _configurationService;

		// TODO: we should memoize this and reset it to null every time the configuration is updated
		public IEnumerable<Label> Labels { get { return GetLabelsFromConfiguration(); } }

		public LabelsService([NotNull] IConfigurationService configurationService)
		{
			_configurationService = configurationService;
		}

		[NotNull]
		private IEnumerable<Label> GetLabelsFromConfiguration()
		{
			var labelsString = _configurationService.Configuration.Labels;
			if (String.IsNullOrEmpty(labelsString))
				return Enumerable.Empty<Label>();

			try
			{
				var labels = labelsString
					.Trim()
					.Trim(';')
					.Split(';')
					.Select(CreateLabelFromString)
					.GroupBy(label => label.Type)
					.Select(labelGrouping => labelGrouping.Last())
					.Take(MaxLabels)
					.ToList();

				if (labels.Count == MaxLabels)
					Log.WarnFormat("Maximum number of labels reached, some may have been dropped.");

				return labels;
			}
			catch (Exception exception)
			{
				Log.WarnFormat("Failed to parse labels configuration string: {0}", exception);
				return Enumerable.Empty<Label>();
			}
		}

		[NotNull]
		private static Label CreateLabelFromString([NotNull] String typeAndValueString)
		{
			if (typeAndValueString == null)
				throw new ArgumentNullException("typeAndValueString");

			var typeAndValueArray = typeAndValueString.Split(':');
			if (typeAndValueArray.Length != 2)
				throw new FormatException("Expected colon separated string but received " + typeAndValueString);

			var type = typeAndValueArray[0];
			if (type == null)
				throw new NullReferenceException("type");

			var value = typeAndValueArray[1];
			if (value == null)
				throw new NullReferenceException("value");

			var typeTrimmed = type.Trim();
			if (typeTrimmed == String.Empty)
				throw new FormatException("Expected colon separated string containing a non-empty first item but received " + typeTrimmed);

			var valueTrimmed = value.Trim();
			if (valueTrimmed == String.Empty)
				throw new FormatException("Expected colon separated string containing a non-empty second item but received " + valueTrimmed);

			var typeTruncated = Truncate(typeTrimmed);
			var valueTruncated = Truncate(valueTrimmed);

			return new Label(typeTruncated, valueTruncated);
		}

		[NotNull]
		private static String Truncate([NotNull] String value)
		{
			var result = value.TruncateUnicode(MaxLength);
			if (result.Length != value.Length)
				Log.WarnFormat("Truncated label key from {0} to {1}", value, result);

			return result;
		}

		public void Dispose()
		{
			// do nothing, just need to implement it to meet the requirements of the interface
		}
	}
}
