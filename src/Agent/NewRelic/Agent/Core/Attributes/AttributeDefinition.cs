// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NewRelic.Agent.Extensions.SystemExtensions;
using NewRelic.Agent.Extensions.Logging;
using System.Diagnostics;

namespace NewRelic.Agent.Core.Attributes
{
    public static class AttributeDefinitionBuilder
    {
        private const int StringValueMaxLengthBytes = 255;
        private const int ErrorMessageMaxLengthBytes = 1023;

        public static AttributeDefinitionBuilder<TInput, TOutput> Create<TInput, TOutput>(string name, AttributeClassification classification)
        {
            return new AttributeDefinitionBuilder<TInput, TOutput>(name, classification);
        }

        public static AttributeDefinitionBuilder<TValue, TValue> Create<TValue>(string name, AttributeClassification classification)
        {
            return new AttributeDefinitionBuilder<TValue, TValue>(name, classification)
                .WithConvert((i) => i);
        }

        public static AttributeDefinitionBuilder<TInput, string> CreateString<TInput>(string name, AttributeClassification classification)
        {
            return new AttributeDefinitionBuilder<TInput, string>(name, classification)
                .WithPostProcessing((v) => v.TruncateUnicodeStringByBytes(StringValueMaxLengthBytes));
        }

        public static AttributeDefinitionBuilder<string, string> CreateString(string name, AttributeClassification classification)
        {
            return Create<string>(name, classification)
                .WithPostProcessing((v) => v.TruncateUnicodeStringByBytes(StringValueMaxLengthBytes));
        }

        public static AttributeDefinitionBuilder<string, string> CreateErrorMessage(string name, AttributeClassification classification)
        {
            return Create<string>(name, classification)
                .WithPostProcessing((v) => v.TruncateUnicodeStringByBytes(ErrorMessageMaxLengthBytes));
        }

        public static AttributeDefinitionBuilder<TInput, double> CreateDouble<TInput>(string name, AttributeClassification classification)
        {
            return new AttributeDefinitionBuilder<TInput, double>(name, classification);
        }

        public static AttributeDefinitionBuilder<double, double> CreateDouble(string name, AttributeClassification classification)
        {
            return Create<double>(name, classification);
        }

        public static AttributeDefinitionBuilder<TInput, bool> CreateBool<TInput>(string name, AttributeClassification classification)
        {
            return new AttributeDefinitionBuilder<TInput, bool>(name, classification);
        }

        public static AttributeDefinitionBuilder<bool, bool> CreateBool(string name, AttributeClassification classification)
        {
            return Create<bool>(name, classification);
        }

        public static AttributeDefinitionBuilder<TInput, long> CreateLong<TInput>(string name, AttributeClassification classification)
        {
            return new AttributeDefinitionBuilder<TInput, long>(name, classification);
        }

        public static AttributeDefinitionBuilder<long, long> CreateLong(string name, AttributeClassification classification)
        {
            return Create<long>(name, classification);
        }

        public static AttributeDefinitionBuilder<string, string> CreateDBStatement(string name, AttributeClassification classification)
        {
            const int dbStmtMaxLength = 1999;

            return new AttributeDefinitionBuilder<string, string>(name, classification)
                .WithConvert((dbStmt) => TruncateDatastoreStatement(dbStmt, dbStmtMaxLength));
        }

        public static AttributeDefinitionBuilder<object, object> CreateCustomAttribute(string name, AttributeDestinations destination)
        {
            var builder = Create<object, object>(name, AttributeClassification.UserAttributes);

            builder.AppliesTo(destination, true);

            builder.WithConvert((input) =>
            {
                if (input == null)
                {
                    return null;
                }

                if (input is TimeSpan)
                {
                    return ((TimeSpan)input).TotalSeconds;
                }
                else if (input is DateTimeOffset)
                {
                    return ((DateTimeOffset)input).ToString("o");
                }

                switch (Type.GetTypeCode(input.GetType()))
                {
                    case TypeCode.SByte:
                    case TypeCode.Byte:
                    case TypeCode.UInt16:
                    case TypeCode.UInt32:
                    case TypeCode.UInt64:
                    case TypeCode.Int16:
                    case TypeCode.Int32:
                        return Convert.ToInt64(input);

                    case TypeCode.Decimal:
                    case TypeCode.Single:
                        return Convert.ToDouble(input);

                    case TypeCode.Double:
                    case TypeCode.Int64:
                    case TypeCode.Boolean:
                    case TypeCode.String:
                        return input;

                    case TypeCode.DateTime:
                        return ((DateTime)input).ToString("o");
                }

                return input.ToString();
            });

            return builder;
        }

        private static string TruncateDatastoreStatement(string statement, int maxSizeBytes)
        {
            const int maxBytesPerUtf8Char = 4;
            const byte firstByte = 0b11000000;
            const byte highBit = 0b10000000;

            var maxCharactersWillFitWithoutTruncation = maxSizeBytes / maxBytesPerUtf8Char;

            if (statement.Length <= maxCharactersWillFitWithoutTruncation)
            {
                return statement;
            }

            var byteArray = Encoding.UTF8.GetBytes(statement);

            if (byteArray.Length <= maxSizeBytes)
            {
                return statement;
            }

            var actualMaxStatementLength = maxSizeBytes - 3;

            var byteOffset = actualMaxStatementLength;

            // Check high bit to see if we're [potentially] in the middle of a multi-byte char
            if ((byteArray[byteOffset] & highBit) == highBit)
            {
                // If so, keep walking back until we have a byte starting with `11`,
                // which means the first byte of a multi-byte UTF8 character.
                while (firstByte != (byteArray[byteOffset] & firstByte))
                {
                    byteOffset--;
                }
            }

            return Encoding.UTF8.GetString(byteArray, 0, byteOffset) + "...";
        }
    }

    public class AttributeDefinitionBuilder<TInput, TOutput>
    {
        private static readonly AttributeDestinations[] AttribDestinationValues =
            Enum.GetValues(typeof(AttributeDestinations)).OfType<AttributeDestinations>()
            .Where(x=>x != AttributeDestinations.None)
            .Where(x=>x != AttributeDestinations.All)
            .ToArray();

        private readonly string _name;
        private readonly AttributeClassification _classification;
        private TOutput _defaultOutputVal;
        private TInput _defaultInputVal;
        private Dictionary<AttributeDestinations, bool> _availability = new Dictionary<AttributeDestinations, bool>();
        private Func<TInput, TOutput> _conversionImpl;
        private Func<TOutput, TOutput> _postProcessingImpl = (o) => o;

        private AttributeDestinations _destinations = AttributeDestinations.None;

        public AttributeDefinitionBuilder(string name, AttributeClassification classification)
        {
            _name = name;
            _classification = classification;
        }

        public AttributeDefinitionBuilder<TInput, TOutput> WithDefaultOutputValue(TOutput defaultOutputVal)
        {
            _defaultOutputVal = defaultOutputVal;
            return this;
        }

        public AttributeDefinitionBuilder<TInput, TOutput> WithDefaultInputValue(TInput defaultInputVal)
        {
            _defaultInputVal = defaultInputVal;
            return this;
        }

        public AttributeDefinitionBuilder<TInput, TOutput> AppliesTo(params AttributeDestinations[] destinations)
        {
            foreach (var destination in destinations)
            {
                AppliesTo(destination, true);
            }

            return this;
        }

        public AttributeDefinitionBuilder<TInput, TOutput> AppliesTo(AttributeDestinations destination, bool isAvailable)
        {
            foreach (var val in AttribDestinationValues)
            {
                if ((destination & val) == val)
                {
                    _availability[val] = isAvailable;
                }
            }

            UpdateDestinationsFlags();
            
            return this;
        }

        private void UpdateDestinationsFlags()
        {
            _destinations = AttributeDestinations.None;

            foreach (var availDestination in _availability.Where(x => x.Value))
            {
                _destinations |= availDestination.Key;
            }
        }

        public AttributeDefinitionBuilder<TInput, TOutput> WithConvert(Func<TInput, TOutput> convertValueImpl)
        {
            _conversionImpl = convertValueImpl;
            return this;
        }

        public AttributeDefinitionBuilder<TInput, TOutput> WithPostProcessing(Func<TOutput, TOutput> postProcessingImpl)
        {
            _postProcessingImpl = postProcessingImpl;
            return this;
        }

        public AttributeDefinition<TInput, TOutput> Build(IAttributeFilter filter)
        {
            if (_defaultInputVal != null && _defaultOutputVal == null && _conversionImpl != null)
            {
                _defaultOutputVal = _conversionImpl(_defaultInputVal);
            }

            // Update the user specified filter to include information 
            // from the declaration and any inforamtion obtained from configuration
            // via the filtering services.
            if (_classification != AttributeClassification.Intrinsics)
            {
                foreach (var dest in _availability.Keys.ToArray())
                {
                    _availability[dest] = _availability[dest]
                        && !filter.ShouldFilterAttribute(dest)
                        && filter.CheckOrAddAttributeClusionCache(_name, dest, dest);
                }

                UpdateDestinationsFlags();
            }

            var result = new AttributeDefinition<TInput, TOutput>(_name, _classification, _availability, _conversionImpl, _defaultOutputVal, _postProcessingImpl);

            return result;
        }
    }

    [DebuggerDisplay("{Name}-{Classification}")]
    public class AttributeDefinition
    {
        public const string KeyName_Guid = "guid";
        public const string KeyName_TraceId = "traceId";

        private const int _attribNameMaxLengthBytes = 255;

        public readonly Guid Guid = Guid.NewGuid();
        public readonly string Name;
        public readonly AttributeClassification Classification;
        public readonly AttributeDestinations AttributeDestinations;
        protected readonly Dictionary<AttributeDestinations, bool> _availability;

        public AttributeDefinition(string name, AttributeClassification classification, Dictionary<AttributeDestinations, bool> availability)
        {
            Name = name;
            Classification = classification;
            _availability = availability;

            foreach (var k in availability.Where(x=>x.Value))
            {
                AttributeDestinations |= k.Key;
            }
        }

        public bool IsAvailableForAny(params AttributeDestinations[] targetModels)
        {
            if(targetModels == null)
            {
                return false;
            }

            foreach(var targetModel in targetModels)
            {
                if(_availability.TryGetValue(targetModel, out var isAvail) && isAvail)
                {
                    return true;
                }
            }

            return false;
        }


        private bool? _isDefinitionValid;
        public bool IsDefinitionValid => (_isDefinitionValid ?? (_isDefinitionValid = ValidateDefinition()).Value);

        private bool ValidateDefinition()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                Log.Debug($"{AttributeDestinations} {Classification} Attribute definition is not valid - Name is null/empty");
                return false;
            }

            if (Encoding.UTF8.GetByteCount(Name) > _attribNameMaxLengthBytes)
            {
                Log.Debug($"{AttributeDestinations} {Classification} Attribute definition is not valid - Name is is too large ({Name})");
                return false;
            }

            return true;
        }

        protected void HandleNullValue()
        {
            if (Classification != AttributeClassification.UserAttributes)
            {
                return;
            }

            Log.Debug($"{AttributeDestinations} {Classification} Attribute '{Name}' was not recorded - value was null");
        }
    }

    public class AttributeDefinition<TInput, TOutput> : AttributeDefinition
    {
        public AttributeDefinition(string name, AttributeClassification classification, Dictionary<AttributeDestinations, bool> availability, Func<TInput, TOutput> conversionImpl, TOutput defaultOutputVal, Func<TOutput, TOutput> postProcessingImpl)
            : base(name, classification, availability)
        {
            _conversionImpl = conversionImpl;
            _defaultOutput = defaultOutputVal;
            _postProcessingImpl = postProcessingImpl;
        }

        private readonly Func<TInput, TOutput> _conversionImpl;
        private readonly TOutput _defaultOutput;
        private readonly Func<TOutput, TOutput> _postProcessingImpl;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="getValFx">Function in users code to obtain the value.  Wrapped in a delegate
        /// so as to not run if it is deemed not necessary</param>
        /// <returns>true if the value was set</returns>
        public bool TrySetValue(IAttributeValueCollection collection, Func<TInput> getInputValFx)
        {
            if (!IsDefinitionValid || collection.IsImmutable || !IsAvailableForAny(collection.TargetModelTypes))
            {
                return false;
            }

            if (getInputValFx == null || _conversionImpl == null)
            {
                HandleNullValue();
                return false;
            }

            return collection.TrySetValue(this, new Lazy<object>(ResolveValue));

            object ResolveValue()
            {
                return ResolveLazyValue(getInputValFx);
            }
        }

        /// <summary>
        /// This method should only be used when the value is simple and available.
        /// any other implementation should use the delegate version of this method
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="sourceVal">The value in it's native form</param>
        /// <returns>true if the value was set</returns>
        public bool TrySetValue(IAttributeValueCollection collection, TInput sourceVal)
        {
            if (!IsDefinitionValid || collection.IsImmutable || !IsAvailableForAny(collection.TargetModelTypes))
            {
                return false;
            }

            if (sourceVal == null || _conversionImpl == null)
            {
                HandleNullValue();
                return false;
            }

            var destVal = _conversionImpl(sourceVal);
            if (destVal == null)
            {
                HandleNullValue();
                return false;
            }

            if (_postProcessingImpl != null)
            {
                destVal = _postProcessingImpl(destVal);
                if (destVal == null)
                {
                    HandleNullValue();
                    return false;
                }
            }

            return collection.TrySetValue(this, destVal);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="collection"></param>
        /// <returns>true if the value was set</returns>
        public bool TrySetDefault(IAttributeValueCollection collection)
        {
            if (collection.IsImmutable)
            {
                return false;
            }

            if (!IsAvailableForAny(collection.TargetModelTypes))
            {
                return false;
            }

            if (_defaultOutput != null)
            {
                return collection.TrySetValue(this, _defaultOutput);
            }

            return false;
        }

        private object ResolveLazyValue(Func<TInput> getInputValFx)
        {
            var inputVal = getInputValFx();
            if (inputVal == null)
            {
                HandleNullValue();
                return null;
            }

            var destVal = _conversionImpl(inputVal);
            if (destVal == null)
            {
                HandleNullValue();
                return null;
            }

            if (_postProcessingImpl != null)
            {
                destVal = _postProcessingImpl(destVal);
                if (destVal == null)
                {
                    HandleNullValue();
                    return null;
                }
            }

            return destVal;
        }
    }
}
