// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Helpers;
using NewRelic.SystemExtensions;

namespace NewRelic.Agent.Core.Labels
{
    public class LabelsService : ILabelsService
    {
        private readonly Serilog.ILogger Log = Serilog.Log.Logger;

        private const int MaxLabels = 64;
        private const int MaxLength = 255;

        private readonly IConfigurationService _configurationService;

        public IEnumerable<Label> Labels { get { return GetLabelsFromConfiguration(); } }

        public LabelsService(IConfigurationService configurationService)
        {
            _configurationService = configurationService;
        }

        private IEnumerable<Label> GetLabelsFromConfiguration()
        {
            var labelsString = _configurationService.Configuration.Labels;
            if (string.IsNullOrEmpty(labelsString))
                return Enumerable.Empty<Label>();

            try
            {
                var labels = labelsString
                    .Trim()
                    .Trim(StringSeparators.SemiColon)
                    .Split(StringSeparators.SemiColon)
                    .Select(CreateLabelFromString)
                    .GroupBy(label => label.Type)
                    .Select(labelGrouping => labelGrouping.Last())
                    .Take(MaxLabels)
                    .ToList();

                if (labels.Count == MaxLabels)
                    Log.Warning("Maximum number of labels reached, some may have been dropped.");

                return labels;
            }
            catch (Exception exception)
            {
                Log.Warning(exception, "Failed to parse labels configuration string");
                return Enumerable.Empty<Label>();
            }
        }

        private Label CreateLabelFromString(string typeAndValueString)
        {
            if (typeAndValueString == null)
                throw new ArgumentNullException("typeAndValueString");

            var typeAndValueArray = typeAndValueString.Split(StringSeparators.Colon);
            if (typeAndValueArray.Length != 2)
                throw new FormatException("Expected colon separated string but received " + typeAndValueString);

            var type = typeAndValueArray[0];
            if (type == null)
                throw new NullReferenceException("type");

            var value = typeAndValueArray[1];
            if (value == null)
                throw new NullReferenceException("value");

            var typeTrimmed = type.Trim();
            if (typeTrimmed == string.Empty)
                throw new FormatException("Expected colon separated string containing a non-empty first item but received " + typeTrimmed);

            var valueTrimmed = value.Trim();
            if (valueTrimmed == string.Empty)
                throw new FormatException("Expected colon separated string containing a non-empty second item but received " + valueTrimmed);

            var typeTruncated = Truncate(typeTrimmed);
            var valueTruncated = Truncate(valueTrimmed);

            return new Label(typeTruncated, valueTruncated);
        }

        private string Truncate(string value)
        {
            var result = value.TruncateUnicodeStringByLength(MaxLength);
            if (result.Length != value.Length)
                Log.Warning("Truncated label key from {0} to {1}", value, result);

            return result;
        }

        public void Dispose()
        {
            // do nothing, just need to implement it to meet the requirements of the interface
        }
    }
}
