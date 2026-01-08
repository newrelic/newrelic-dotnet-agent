// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Utilities;

namespace NewRelic.Agent.Core.OpenTelemetryBridge
{
    public class MeterBridgingService : DisposableService, IMeterBridgingService
    {
        private readonly IMeterListenerWrapper _meterListener;
        private readonly IConfigurationService _configurationService;
        private readonly IOtelBridgeSupportabilityMetricCounters _supportabilityMetricCounters;

        private readonly Meter _newRelicBridgeMeter = new Meter("NewRelicOTelBridgeMeter");
        private readonly ConcurrentDictionary<string, Meter> _bridgedMeters = new ConcurrentDictionary<string, Meter>();
        // NOTE: These caches are unbounded and grow with the number of unique instrument types encountered.
        // This is acceptable because the set of instrument types is typically small and bounded by application code.
        private readonly ConcurrentDictionary<Type, object> _createInstrumentDelegates = new ConcurrentDictionary<Type, object>();
        private readonly ConcurrentDictionary<Type, ObservableInstrumentCacheData> _createObservableInstrumentCache = new ConcurrentDictionary<Type, ObservableInstrumentCacheData>();
        private readonly ConcurrentDictionary<Type, PropertyAccessorCache> _propertyAccessorCache = new ConcurrentDictionary<Type, PropertyAccessorCache>();
        private readonly ConcurrentDictionary<Type, Func<object, string>> _meterNameAccessorCache = new ConcurrentDictionary<Type, Func<object, string>>();
        private readonly ConcurrentDictionary<Type, MeasurementAccessors> _measurementAccessorsCache = new ConcurrentDictionary<Type, MeasurementAccessors>();

        public MeterBridgingService(
            IMeterListenerWrapper meterListener,
            IConfigurationService configurationService,
            IOtelBridgeSupportabilityMetricCounters supportabilityMetricCounters)
        {
            _meterListener = meterListener;
            _configurationService = configurationService;
            _supportabilityMetricCounters = supportabilityMetricCounters;

            _meterListener.InstrumentPublished = OnInstrumentPublished;
            _meterListener.MeasurementsCompleted = OnMeasurementsCompleted;

            RegisterMeasurementCallbacks();
        }

        public void StartListening(object meter = null)
        {
            // Note: meter parameter is unused but kept for interface compatibility
            _meterListener.Start();
        }

        public void StopListening()
        {
            _meterListener.Dispose();
        }

        private void RegisterMeasurementCallbacks()
        {
            _meterListener.SetMeasurementCallback<byte>(OnMeasurementRecorded);
            _meterListener.SetMeasurementCallback<short>(OnMeasurementRecorded);
            _meterListener.SetMeasurementCallback<int>(OnMeasurementRecorded);
            _meterListener.SetMeasurementCallback<long>(OnMeasurementRecorded);
            _meterListener.SetMeasurementCallback<float>(OnMeasurementRecorded);
            _meterListener.SetMeasurementCallback<double>(OnMeasurementRecorded);
            _meterListener.SetMeasurementCallback<decimal>(OnMeasurementRecorded);
        }

        public void OnInstrumentPublished(object instrument, IMeterListenerWrapper listener)
        {
            try
            {
                if (instrument == null) return;

                var instrumentType = instrument.GetType();
                var accessors = GetOrCreatePropertyAccessors(instrumentType);
                
                var meterObj = accessors.MeterAccessor?.Invoke(instrument);
                if (meterObj == null) return;

                var meterType = meterObj.GetType();
                var meterNameAccessor = _meterNameAccessorCache.GetOrAdd(meterType, type =>
                {
                    var prop = type.GetProperty("Name");
                    if (prop == null) return null;
                    return obj => prop.GetValue(obj) as string;
                });
                
                if (meterNameAccessor == null) return;
                var meterName = meterNameAccessor(meterObj);
                if (meterName == null) return;
                if (string.IsNullOrEmpty(meterName)) return;

                if (MeterFilterHelpers.ShouldEnableInstrumentsInMeter(_configurationService.Configuration, meterName))
                {
                    var state = GetStateForInstrumentWithMetrics(instrument);
                    _meterListener.EnableMeasurementEvents(instrument, state);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error in OnInstrumentPublished callback for instrument type: {instrument?.GetType().Name}");
            }
        }

        private void OnMeasurementsCompleted(object instrument, object state, IMeterListenerWrapper listener)
        {
            if (state is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        private object GetStateForInstrumentWithMetrics(object instrument)
        {
            _supportabilityMetricCounters?.Record(OtelBridgeSupportabilityMetric.GetMeter);

            var result = GetStateForInstrument(instrument);

            if (result != null)
            {
                _supportabilityMetricCounters?.Record(OtelBridgeSupportabilityMetric.InstrumentCreated);
                RecordSpecificInstrumentType(instrument.GetType().Name);
            }
            else
            {
                _supportabilityMetricCounters?.Record(OtelBridgeSupportabilityMetric.InstrumentBridgeFailure);
            }

            return result;
        }

        private object GetStateForInstrument(object instrument)
        {
            var instrumentType = instrument.GetType();
            var genericType = instrumentType.GetGenericArguments().FirstOrDefault();
            if (genericType == null) return null;

            var meterProp = instrumentType.GetProperty("Meter");
            var nameProp = instrumentType.GetProperty("Name");
            var unitProp = instrumentType.GetProperty("Unit");
            var descProp = instrumentType.GetProperty("Description");
            var isObservableProp = instrumentType.GetProperty("IsObservable");

            var meterObj = meterProp?.GetValue(instrument);
            var meterName = meterObj?.GetType().GetProperty("Name")?.GetValue(meterObj) as string;

            Meter meter = _bridgedMeters.GetOrAdd(meterName ?? "Unknown", (_) => CreateBridgedMeter(meterObj));

            var isObservable = (bool?)isObservableProp?.GetValue(instrument) ?? false;
            if (isObservable)
            {
                var cacheData = _createObservableInstrumentCache.GetOrAdd(instrumentType, GetObservableInstrumentCacheData);
                var result = cacheData?.CreateCallbackAndObservableInstrumentDelegate(instrument, meter, cacheData);
                Log.Debug($"Created bridged observable instrument: {nameProp?.GetValue(instrument)} on meter: {meterName}");
                return result;
            }

            var createDelegate = (Func<Meter, string, string, string, IEnumerable<KeyValuePair<string, object>>, object, object>)_createInstrumentDelegates.GetOrAdd(instrumentType, CreateBridgedInstrumentDelegate);
            
            var name = nameProp?.GetValue(instrument) as string;
            var unit = unitProp?.GetValue(instrument) as string;
            var description = descProp?.GetValue(instrument) as string;
            var tags = GetInstrumentTags(instrument);
            var advice = GetInstrumentAdvice(instrument);

            return createDelegate?.Invoke(meter, name, unit, description, tags, advice);
        }

        private IEnumerable<KeyValuePair<string, object>> GetInstrumentTags(object instrument)
        {
            var accessors = GetOrCreatePropertyAccessors(instrument.GetType());
            if (accessors.TagsAccessor == null) return null;
            
            try
            {
                var tagsValue = accessors.TagsAccessor(instrument) as IEnumerable;
                if (tagsValue == null) return null;

                var list = new List<KeyValuePair<string, object>>();
                foreach (var tag in tagsValue)
                {
                    var t = tag.GetType();
                    var k = t.GetProperty("Key")?.GetValue(tag) as string;
                    if (k == null) continue; // Skip tags with null keys
                    var v = t.GetProperty("Value")?.GetValue(tag);
                    list.Add(new KeyValuePair<string, object>(k, v));
                }
                return list.Count > 0 ? list : null;
            }
            catch (Exception ex)
            {
                Log.Debug($"Failed to extract tags from instrument: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Extracts the Advice property from an instrument using reflection.
        /// The Advice property is only available in .NET 9.0/DiagnosticSource 9.0+.
        /// Returns null if the property doesn't exist or cannot be accessed.
        /// </summary>
        private object GetInstrumentAdvice(object instrument)
        {
            try
            {
                // Look for the Advice property on the instrument instance
                var adviceProp = instrument.GetType().GetProperty("Advice");
                return adviceProp?.GetValue(instrument);
            }
            catch (Exception ex)
            {
                // Expected to fail on .NET versions < 9.0 where Advice property doesn't exist
                Log.Finest($"Advice property not available on instrument (expected on .NET < 9.0): {ex.Message}");
                return null;
            }
        }


        private void OnMeasurementRecorded<T>(object instrument, T measurement, ReadOnlySpan<KeyValuePair<string, object>> tags, object state) where T : struct
        {
            if (state is Instrument bridgedInstrument)
            {
                // Filter out tags with null keys to prevent NullReferenceException
                var validTags = FilterValidTags(tags);
                var validTagsSpan = new ReadOnlySpan<KeyValuePair<string, object>>(validTags);

                var handled = false;
                if (bridgedInstrument is Counter<T> counter)
                {
                    counter.Add(measurement, validTagsSpan);
                    handled = true;
                }
                else if (bridgedInstrument is Histogram<T> histogram)
                {
                    histogram.Record(measurement, validTagsSpan);
                    handled = true;
                }
                else if (bridgedInstrument is UpDownCounter<T> upDownCounter)
                {
                    upDownCounter.Add(measurement, validTagsSpan);
                    handled = true;
                }
                
                if (handled)
                {
                    _supportabilityMetricCounters?.Record(OtelBridgeSupportabilityMetric.MeasurementRecorded);
                }
                else
                {
                    _supportabilityMetricCounters?.Record(OtelBridgeSupportabilityMetric.MeasurementBridgeFailure);
                    Log.Debug($"Unsupported bridged instrument type: {bridgedInstrument.GetType().Name}");
                }
            }
        }

        private Meter CreateBridgedMeter(object originalMeter)
        {
            if (originalMeter == null) return _newRelicBridgeMeter;
            var type = originalMeter.GetType();
            var name = type.GetProperty("Name")?.GetValue(originalMeter) as string ?? "Unknown";
            var version = type.GetProperty("Version")?.GetValue(originalMeter) as string;
            var tags = GetInstrumentTags(originalMeter);
            var scope = GetMeterScope(originalMeter);

            // Reflection logic for Meter constructor compatibility across .NET versions:
            // - .NET 9.0+/DiagnosticSource 9.0+: 4-parameter constructor with scope support
            // - .NET 8.0+/DiagnosticSource 8.0+: 3-parameter constructor with tags support
            // - Earlier versions: 2-parameter constructor (name, version) only
            // We attempt each overload in order from newest to oldest to gracefully degrade on older runtimes.
            // Always match by parameter types, not just count, to avoid ambiguity.
            
            // Try 4-parameter constructor (name, version, tags, scope) - .NET 9.0+
            if (scope != null || tags != null)
            {
                var constructor4 = typeof(Meter).GetConstructor(new[] 
                { 
                    typeof(string), 
                    typeof(string), 
                    typeof(IEnumerable<KeyValuePair<string, object>>), 
                    typeof(object) 
                });
                
                if (constructor4 != null)
                {
                    return (Meter)constructor4.Invoke(new object[] { name, version, tags, scope });
                }
            }
            
            // Try 3-parameter constructor (name, version, tags)
            if (tags != null)
            {
                var constructor3 = typeof(Meter).GetConstructor(new[] 
                { 
                    typeof(string), 
                    typeof(string), 
                    typeof(IEnumerable<KeyValuePair<string, object>>) 
                });
                
                if (constructor3 != null)
                {
                    return (Meter)constructor3.Invoke(new object[] { name, version, tags });
                }
            }
            
            // Fallback to 2-parameter constructor (name, version)
            return new Meter(name, version);
        }

        /// <summary>
        /// Extracts the Scope property from a meter using reflection.
        /// The Scope property is only available in .NET 9.0+/DiagnosticSource 9.0+.
        /// Returns null if the property doesn't exist or cannot be accessed.
        /// </summary>
        private object GetMeterScope(object meter)
        {
            if (meter == null) return null;
            
            try
            {
                var scopeProp = meter.GetType().GetProperty("Scope");
                return scopeProp?.GetValue(meter);
            }
            catch (Exception ex)
            {
                // Expected to fail on .NET versions < 9.0 where Scope property doesn't exist
                Log.Finest($"Scope property not available on meter (expected on .NET < 9.0): {ex.Message}");
                return null;
            }
        }

        private void RecordSpecificInstrumentType(string instrumentTypeName)
        {
            switch (instrumentTypeName)
            {
                case "Counter`1":
                    _supportabilityMetricCounters?.Record(OtelBridgeSupportabilityMetric.CreateCounter);
                    break;
                case "Histogram`1":
                    _supportabilityMetricCounters?.Record(OtelBridgeSupportabilityMetric.CreateHistogram);
                    break;
                case "UpDownCounter`1":
                    _supportabilityMetricCounters?.Record(OtelBridgeSupportabilityMetric.CreateUpDownCounter);
                    break;
                case "Gauge`1":
                    _supportabilityMetricCounters?.Record(OtelBridgeSupportabilityMetric.CreateGauge);
                    break;
                case "ObservableCounter`1":
                    _supportabilityMetricCounters?.Record(OtelBridgeSupportabilityMetric.CreateObservableCounter);
                    break;
                case "ObservableGauge`1":
                    _supportabilityMetricCounters?.Record(OtelBridgeSupportabilityMetric.CreateObservableGauge);
                    break;
                case "ObservableUpDownCounter`1":
                    _supportabilityMetricCounters?.Record(OtelBridgeSupportabilityMetric.CreateObservableUpDownCounter);
                    break;
                case "ObservableHistogram`1":
                    _supportabilityMetricCounters?.Record(OtelBridgeSupportabilityMetric.CreateObservableHistogram);
                    break;
            }
        }

        private ObservableInstrumentCacheData GetObservableInstrumentCacheData(Type instrumentType)
        {
            var cacheData = new ObservableInstrumentCacheData();
            cacheData.CreateObservableInstrumentDelegate = CreateBridgedObservableInstrumentDelegate(instrumentType);
            cacheData.CreateCallbackAndObservableInstrumentDelegate = CreateCallbackAndBridgedObservableInstrumentAction(instrumentType);
            cacheData.ObserveMethodDelegate = CreateObserveMethodInvoker(instrumentType);
            cacheData.ServiceInstance = this; // Pass service instance for BridgeMeasurements access
            return cacheData;
        }

        private Func<object, IEnumerable> CreateObserveMethodInvoker(Type instrumentType)
        {
            var observeMethod = instrumentType.GetMethod("Observe", BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
            var param = Expression.Parameter(typeof(object), "instrument");
            var typed = Expression.Convert(param, instrumentType);
            var call = Expression.Call(typed, observeMethod);
            return Expression.Lambda<Func<object, IEnumerable>>(Expression.Convert(call, typeof(IEnumerable)), param).Compile();
        }

        private Func<object, Meter, ObservableInstrumentCacheData, Instrument> CreateCallbackAndBridgedObservableInstrumentAction(Type instrumentType)
        {
            var genericType = instrumentType.GetGenericArguments().First();
            var method = typeof(MeterBridgingService).GetMethod(nameof(CreateCallbackAndBridgedObservableInstrument), BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(genericType);
            return (Func<object, Meter, ObservableInstrumentCacheData, Instrument>)method.CreateDelegate(typeof(Func<object, Meter, ObservableInstrumentCacheData, Instrument>));
        }

        private static Delegate CreateBridgedObservableInstrumentDelegate(Type originalInstrumentType)
        {
            var genericType = originalInstrumentType.GetGenericArguments().FirstOrDefault();
            if (genericType == null)
            {
                return null;
            }

            var createInstrumentMethodName = originalInstrumentType.Name switch
            {
                "ObservableCounter`1" => nameof(CreateBridgedObservableCounter),
                "ObservableGauge`1" => nameof(CreateBridgedObservableGauge),
                "ObservableUpDownCounter`1" => nameof(CreateBridgedObservableUpDownCounter),
                _ => null
            };

            if (createInstrumentMethodName == null)
            {
                return null;
            }

            var delegateType = typeof(CreateObservableInstrumentDelegate<>).MakeGenericType(genericType);
            var methodInfo = typeof(MeterBridgingService).GetMethod(createInstrumentMethodName, BindingFlags.NonPublic | BindingFlags.Static)?.MakeGenericMethod(genericType);
            if (methodInfo == null)
            {
                return null;
            }

            return methodInfo.CreateDelegate(delegateType);
        }

        private delegate Instrument CreateObservableInstrumentDelegate<T>(Meter meter, string name, Func<IEnumerable<Measurement<T>>> callback, string unit, string description) where T : struct;

        private static Instrument CreateBridgedObservableCounter<T>(Meter meter, string name, Func<IEnumerable<Measurement<T>>> callback, string unit, string description) where T : struct
        {
            return meter.CreateObservableCounter(name, callback, unit, description);
        }

        private static Instrument CreateBridgedObservableGauge<T>(Meter meter, string name, Func<IEnumerable<Measurement<T>>> callback, string unit, string description) where T : struct
        {
            return meter.CreateObservableGauge(name, callback, unit, description);
        }

        private static Instrument CreateBridgedObservableUpDownCounter<T>(Meter meter, string name, Func<IEnumerable<Measurement<T>>> callback, string unit, string description) where T : struct
        {
            return meter.CreateObservableUpDownCounter(name, callback, unit, description);
        }

        private static Instrument CreateCallbackAndBridgedObservableInstrument<T>(object instrument, Meter meter, ObservableInstrumentCacheData cacheData) where T : struct
        {
            var createObservableInstrument = (CreateObservableInstrumentDelegate<T>)cacheData.CreateObservableInstrumentDelegate;
            if (createObservableInstrument == null)
            {
                return null;
            }

            var type = instrument.GetType();
            var name = type.GetProperty("Name")?.GetValue(instrument) as string;
            var unit = type.GetProperty("Unit")?.GetValue(instrument) as string;
            var desc = type.GetProperty("Description")?.GetValue(instrument) as string;

            var observeMethod = cacheData.ObserveMethodDelegate;
            var serviceInstance = cacheData.ServiceInstance;
            return createObservableInstrument(meter, name, ForwardObservedMeasurements, unit, desc);

            IEnumerable<Measurement<T>> ForwardObservedMeasurements()
            {
                return serviceInstance.BridgeMeasurements<T>(observeMethod(instrument));
            }
        }

        private IEnumerable<Measurement<T>> BridgeMeasurements<T>(IEnumerable originalMeasurements) where T : struct
        {
            var list = new List<Measurement<T>>();
            try
            {
                MeasurementAccessors accessors = null;
                
                foreach (var m in originalMeasurements)
                {
                    // Cache accessors on first iteration to avoid repeated reflection
                    if (accessors == null)
                    {
                        var measurementType = m.GetType();
                        accessors = _measurementAccessorsCache.GetOrAdd(measurementType, type =>
                        {
                            // Compatibility workaround: Access private fields to support Measurement<T> across different .NET versions
                            // The Measurement<T> struct layout may vary between .NET Framework, .NET Core, and newer .NET versions.
                            // We try private fields first (more reliable for value types), then fall back to public properties.
                            var valueField = type.GetField("_value", BindingFlags.NonPublic | BindingFlags.Instance);
                            var valueProp = type.GetProperty("Value");
                            var tagsField = type.GetField("_tags", BindingFlags.NonPublic | BindingFlags.Instance);
                            var tagsProp = type.GetProperty("Tags");
                            
                            return new MeasurementAccessors
                            {
                                ValueAccessor = valueField != null 
                                    ? (Func<object, object>)(obj => valueField.GetValue(obj))
                                    : (valueProp != null ? (obj => valueProp.GetValue(obj)) : null),
                                TagsAccessor = tagsField != null
                                    ? (Func<object, IEnumerable<KeyValuePair<string, object>>>)(obj => tagsField.GetValue(obj) as IEnumerable<KeyValuePair<string, object>>)
                                    : (tagsProp != null ? (obj => tagsProp.GetValue(obj) as IEnumerable<KeyValuePair<string, object>>) : null)
                            };
                        });
                    }
                    
                    var val = accessors.ValueAccessor != null ? (T)accessors.ValueAccessor(m) : default(T);
                    var tags = accessors.TagsAccessor?.Invoke(m);
                    
                    // Filter out tags with null keys
                    var validTags = tags?.Where(tag => tag.Key != null);
                    
                    list.Add(new Measurement<T>(val, validTags));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error bridging observable instrument measurements. Returning partial results.");
            }
            return list;
        }

        private object CreateBridgedInstrumentDelegate(Type instrumentType)
        {
            var genericType = instrumentType.GetGenericArguments().First();
            var method = typeof(MeterBridgingService).GetMethod(nameof(CreateBridgedInstrumentInternal), BindingFlags.NonPublic | BindingFlags.Instance).MakeGenericMethod(genericType);
            
            var meterParam = Expression.Parameter(typeof(Meter), "meter");
            var nameParam = Expression.Parameter(typeof(string), "name");
            var unitParam = Expression.Parameter(typeof(string), "unit");
            var descParam = Expression.Parameter(typeof(string), "desc");
            var tagsParam = Expression.Parameter(typeof(IEnumerable<KeyValuePair<string, object>>), "tags");
            var adviceParam = Expression.Parameter(typeof(object), "advice");
            
            var call = Expression.Call(Expression.Constant(this), method, Expression.Constant(instrumentType), meterParam, nameParam, unitParam, descParam, tagsParam, adviceParam);
            return Expression.Lambda(call, meterParam, nameParam, unitParam, descParam, tagsParam, adviceParam).Compile();
        }

        private object CreateBridgedInstrumentInternal<T>(Type instrumentType, Meter meter, string name, string unit, string desc, IEnumerable<KeyValuePair<string, object>> tags, object advice) where T : struct
        {
            var createInstrumentMethodName = instrumentType.Name switch
            {
                "Histogram`1" => "CreateHistogram",
                "UpDownCounter`1" => "CreateUpDownCounter",
                "Counter`1" => "CreateCounter",
                _ => null
            };

            if (createInstrumentMethodName == null)
            {
                return null;
            }

            // Reflection logic for Meter.Create* methods across .NET versions:
            // - .NET 9.0+/DiagnosticSource 9.0+: 5-parameter overload with InstrumentAdvice support
            // - .NET 8.0+/DiagnosticSource 8.0+: 4-parameter overload with tags support
            // - Earlier versions: 3-parameter overload (name, unit, description) only
            // We attempt each overload in order from newest to oldest to gracefully degrade on older runtimes.
            // Note: We match InstrumentAdvice by name (not type) because it may not exist in earlier versions.
            // Always match by parameter types, not just count, to avoid ambiguity.
            // See: https://github.com/dotnet/runtime/blob/main/src/libraries/System.Diagnostics.DiagnosticSource/src/System/Diagnostics/Metrics/Meter.cs
            
            // Try 5-parameter overload (name, unit, description, tags, advice) - .NET 9.0+
            var method5Param = FindCreateInstrumentMethod(
                createInstrumentMethodName,
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(IEnumerable<KeyValuePair<string, object>>),
                "InstrumentAdvice"); // Match by name for compatibility
            
            if (method5Param != null)
            {
                var genericMethod = method5Param.MakeGenericMethod(typeof(T));
                // Pass the advice parameter we extracted
                return genericMethod.Invoke(meter, new object[] { name, unit, desc, tags, advice });
            }
            
            // Try 4-parameter overload (name, unit, description, tags) - .NET 8.0+
            var method4Param = FindCreateInstrumentMethod(
                createInstrumentMethodName,
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(IEnumerable<KeyValuePair<string, object>>));
            
            if (method4Param != null)
            {
                var genericMethod = method4Param.MakeGenericMethod(typeof(T));
                return genericMethod.Invoke(meter, new object[] { name, unit, desc, tags });
            }

            // Fallback: Try 3-parameter overload (name, unit, description) - Earlier versions
            var method3Param = FindCreateInstrumentMethod(
                createInstrumentMethodName,
                typeof(string),
                typeof(string),
                typeof(string));

            if (method3Param != null)
            {
                var genericMethod = method3Param.MakeGenericMethod(typeof(T));
                return genericMethod.Invoke(meter, new object[] { name, unit, desc });
            }

            return null;
        }

        public override void Dispose()
        {
            _meterListener.Dispose();
            _newRelicBridgeMeter.Dispose();
            foreach (var meter in _bridgedMeters.Values) meter.Dispose();
            base.Dispose();
        }

        #region Helper Methods

        /// <summary>
        /// Filters tags to remove entries with null keys, preventing NullReferenceExceptions.
        /// Optimized to avoid double array allocation.
        /// </summary>
        private static KeyValuePair<string, object>[] FilterValidTags(ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            // Single-pass: count valid tags, allocate once, populate
            var count = 0;
            for (var i = 0; i < tags.Length; i++)
            {
                if (tags[i].Key != null) count++;
            }
            
            if (count == 0) return Array.Empty<KeyValuePair<string, object>>();
            if (count == tags.Length) return tags.ToArray(); // All valid, direct copy
            
            var result = new KeyValuePair<string, object>[count];
            var index = 0;
            for (var i = 0; i < tags.Length; i++)
            {
                if (tags[i].Key != null)
                {
                    result[index++] = tags[i];
                }
            }
            return result;
        }

        /// <summary>
        /// Finds a Create* method on the Meter type that matches the specified name and parameter types.
        /// Supports matching by parameter type name for types that may not exist in earlier .NET versions.
        /// </summary>
        private MethodInfo FindCreateInstrumentMethod(string methodName, params object[] parameterTypesOrNames)
        {
            return typeof(Meter).GetMethods()
                .FirstOrDefault(m =>
                {
                    if (m.Name != methodName || !m.IsGenericMethod)
                        return false;

                    var parameters = m.GetParameters();
                    if (parameters.Length != parameterTypesOrNames.Length)
                        return false;

                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (parameterTypesOrNames[i] is Type expectedType)
                        {
                            if (parameters[i].ParameterType != expectedType)
                                return false;
                        }
                        else if (parameterTypesOrNames[i] is string expectedName)
                        {
                            if (parameters[i].ParameterType.Name != expectedName)
                                return false;
                        }
                    }

                    return true;
                });
        }

        /// <summary>
        /// Gets or creates cached property accessors for an instrument type to improve reflection performance.
        /// Uses compiled expression trees for better performance than closure-based accessors.
        /// </summary>
        private PropertyAccessorCache GetOrCreatePropertyAccessors(Type instrumentType)
        {
            return _propertyAccessorCache.GetOrAdd(instrumentType, type =>
            {
                var cache = new PropertyAccessorCache();
                try
                {
                    cache.MeterAccessor = CreatePropertyAccessor(type, "Meter");
                    cache.NameAccessor = CreateTypedPropertyAccessor<string>(type, "Name");
                    cache.UnitAccessor = CreateTypedPropertyAccessor<string>(type, "Unit");
                    cache.DescriptionAccessor = CreateTypedPropertyAccessor<string>(type, "Description");
                    cache.TagsAccessor = CreatePropertyAccessor(type, "Tags");
                }
                catch (Exception ex)
                {
                    Log.Debug($"Failed to create property accessors for type {type.Name}: {ex.Message}");
                }
                return cache;
            });
        }

        /// <summary>
        /// Creates a compiled property accessor using expression trees for optimal performance.
        /// </summary>
        private static Func<object, object> CreatePropertyAccessor(Type type, string propertyName)
        {
            var prop = type.GetProperty(propertyName);
            if (prop == null) return null;

            var param = Expression.Parameter(typeof(object), "obj");
            var typedParam = Expression.Convert(param, type);
            var propertyAccess = Expression.Property(typedParam, prop);
            var convertToObject = Expression.Convert(propertyAccess, typeof(object));
            
            return Expression.Lambda<Func<object, object>>(convertToObject, param).Compile();
        }

        /// <summary>
        /// Creates a compiled typed property accessor using expression trees.
        /// </summary>
        private static Func<object, T> CreateTypedPropertyAccessor<T>(Type type, string propertyName)
        {
            var prop = type.GetProperty(propertyName);
            if (prop == null) return null;

            var param = Expression.Parameter(typeof(object), "obj");
            var typedParam = Expression.Convert(param, type);
            var propertyAccess = Expression.Property(typedParam, prop);
            var convertToType = Expression.Convert(propertyAccess, typeof(T));
            
            return Expression.Lambda<Func<object, T>>(convertToType, param).Compile();
        }

        #endregion

        internal class PropertyAccessorCache
        {
            public Func<object, object> MeterAccessor { get; set; }
            public Func<object, string> NameAccessor { get; set; }
            public Func<object, string> UnitAccessor { get; set; }
            public Func<object, string> DescriptionAccessor { get; set; }
            public Func<object, object> TagsAccessor { get; set; }
        }

        internal class MeasurementAccessors
        {
            public Func<object, object> ValueAccessor { get; set; }
            public Func<object, IEnumerable<KeyValuePair<string, object>>> TagsAccessor { get; set; }
        }

        internal class ObservableInstrumentCacheData
        {
            public Delegate CreateObservableInstrumentDelegate { get; set; }
            public Func<object, Meter, ObservableInstrumentCacheData, Instrument> CreateCallbackAndObservableInstrumentDelegate { get; set; }
            public Func<object, IEnumerable> ObserveMethodDelegate { get; set; }
            public MeterBridgingService ServiceInstance { get; set; }
        }
    }
}
