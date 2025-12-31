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
using System.IO;
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
        private readonly MeterFilterService _filterService = new MeterFilterService();

        private readonly Meter _newRelicBridgeMeter = new Meter("NewRelicOTelBridgeMeter");
        private readonly ConcurrentDictionary<string, Meter> _bridgedMeters = new ConcurrentDictionary<string, Meter>();
        private readonly ConcurrentDictionary<Type, object> _createInstrumentDelegates = new ConcurrentDictionary<Type, object>();
        private readonly ConcurrentDictionary<Type, ObservableInstrumentCacheData> _createObservableInstrumentCache = new ConcurrentDictionary<Type, ObservableInstrumentCacheData>();

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

        public void StartListening(object meter)
        {
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
                var instrumentType = instrument.GetType();
                var meterProp = instrumentType.GetProperty("Meter");
                var meterObj = meterProp?.GetValue(instrument);
                if (meterObj == null) return;

                var meterName = meterObj.GetType().GetProperty("Name")?.GetValue(meterObj) as string;
                if (string.IsNullOrEmpty(meterName)) return;

                if (_filterService.ShouldEnableInstrumentsInMeter(_configurationService.Configuration, meterName))
                {
                    var state = GetStateForInstrumentWithMetrics(instrument);
                    _meterListener.EnableMeasurementEvents(instrument, state);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in OnInstrumentPublished callback");
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

            var createDelegate = (Func<Meter, string, string, string, IEnumerable<KeyValuePair<string, object>>, object>)_createInstrumentDelegates.GetOrAdd(instrumentType, CreateBridgedInstrumentDelegate);
            
            var name = nameProp?.GetValue(instrument) as string;
            var unit = unitProp?.GetValue(instrument) as string;
            var description = descProp?.GetValue(instrument) as string;
            var tags = GetInstrumentTags(instrument);

            return createDelegate?.Invoke(meter, name, unit, description, tags);
        }

        private IEnumerable<KeyValuePair<string, object>> GetInstrumentTags(object instrument)
        {
            var tagsProp = instrument.GetType().GetProperty("Tags");
            if (tagsProp == null) return null;
            
            try
            {
                var tagsValue = tagsProp.GetValue(instrument) as IEnumerable;
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
            catch { return null; }
        }

        private void OnMeasurementRecorded<T>(object instrument, T measurement, ReadOnlySpan<KeyValuePair<string, object>> tags, object state) where T : struct
        {
            if (state is Instrument bridgedInstrument)
            {
                // Filter out tags with null keys to prevent NullReferenceException
                var validTags = tags.ToArray().Where(t => t.Key != null).ToArray();
                var validTagsSpan = new ReadOnlySpan<KeyValuePair<string, object>>(validTags);

                if (bridgedInstrument is Counter<T> counter)
                {
                    counter.Add(measurement, validTagsSpan);
                }
                else if (bridgedInstrument is Histogram<T> histogram)
                {
                    histogram.Record(measurement, validTagsSpan);
                }
                else if (bridgedInstrument is UpDownCounter<T> upDownCounter)
                {
                    upDownCounter.Add(measurement, validTagsSpan);
                }
                
                _supportabilityMetricCounters?.Record(OtelBridgeSupportabilityMetric.MeasurementRecorded);
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

            // Reflection logic for Meter constructor:
            // 1. Try 4-parameter constructor (name, version, tags, scope) if scope is available
            // 2. Try 3-parameter constructor (name, version, tags) if tags are available
            // 3. Fallback to 2-parameter constructor (name, version) for backward compatibility
            // Always match by parameter types, not just count
            
            // Try 4-parameter constructor (name, version, tags, scope) - newest
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
        /// The Scope property is only available in newer versions of System.Diagnostics.DiagnosticSource.
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
            catch
            {
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
            return createObservableInstrument(meter, name, ForwardObservedMeasurements, unit, desc);

            IEnumerable<Measurement<T>> ForwardObservedMeasurements()
            {
                return BridgeMeasurements<T>(observeMethod(instrument));
            }
        }

        private static IEnumerable<Measurement<T>> BridgeMeasurements<T>(IEnumerable originalMeasurements) where T : struct
        {
            var list = new List<Measurement<T>>();
            try
            {
                foreach (var m in originalMeasurements)
                {
                    var t = m.GetType();
                    
                    // Try to get value using field first (more reliable for structs)
                    var valueField = t.GetField("_value", BindingFlags.NonPublic | BindingFlags.Instance);
                    var val = valueField != null ? (T)valueField.GetValue(m) : (T)t.GetProperty("Value").GetValue(m);
                    
                    // Try to get tags using field first
                    var tagsField = t.GetField("_tags", BindingFlags.NonPublic | BindingFlags.Instance);
                    var tags = tagsField != null 
                        ? tagsField.GetValue(m) as IEnumerable<KeyValuePair<string, object>>
                        : t.GetProperty("Tags")?.GetValue(m) as IEnumerable<KeyValuePair<string, object>>;
                    
                    // Filter out tags with null keys
                    var validTags = tags?.Where(tag => tag.Key != null);
                    
                    list.Add(new Measurement<T>(val, validTags));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error bridging observable instrument measurements");
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
            
            var call = Expression.Call(Expression.Constant(this), method, Expression.Constant(instrumentType), meterParam, nameParam, unitParam, descParam, tagsParam);
            return Expression.Lambda(call, meterParam, nameParam, unitParam, descParam, tagsParam).Compile();
        }

        private object CreateBridgedInstrumentInternal<T>(Type instrumentType, Meter meter, string name, string unit, string desc, IEnumerable<KeyValuePair<string, object>> tags) where T : struct
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

            // Reflection logic for Meter.Create* methods:
            // 1. Try to match the latest public OTel signature (4 parameters: name, unit, description, tags)
            // 2. Fallback to 3-parameter version (name, unit, description) for backward compatibility
            // 3. Always match by parameter types, not just count
            // See: https://github.com/dotnet/runtime/blob/main/src/libraries/System.Diagnostics.DiagnosticSource/src/System/Diagnostics/Metrics/Meter.cs
            
            // Try 4-parameter overload (name, unit, description, tags)
            var method4Param = typeof(Meter).GetMethods()
                .FirstOrDefault(m =>
                    m.Name == createInstrumentMethodName &&
                    m.IsGenericMethod &&
                    m.GetParameters().Select(p => p.ParameterType)
                        .SequenceEqual(new[] { typeof(string), typeof(string), typeof(string), typeof(IEnumerable<KeyValuePair<string, object>>) })
                );
            
            if (method4Param != null)
            {
                var genericMethod = method4Param.MakeGenericMethod(typeof(T));
                return genericMethod.Invoke(meter, new object[] { name, unit, desc, tags });
            }

            // Fallback: Try 3-parameter overload (name, unit, description)
            var method3Param = typeof(Meter).GetMethods()
                .FirstOrDefault(m =>
                    m.Name == createInstrumentMethodName &&
                    m.IsGenericMethod &&
                    m.GetParameters().Select(p => p.ParameterType)
                        .SequenceEqual(new[] { typeof(string), typeof(string), typeof(string) })
                );

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

        internal class ObservableInstrumentCacheData
        {
            public Delegate CreateObservableInstrumentDelegate { get; set; }
            public Func<object, Meter, ObservableInstrumentCacheData, Instrument> CreateCallbackAndObservableInstrumentDelegate { get; set; }
            public Func<object, IEnumerable> ObserveMethodDelegate { get; set; }
        }
    }
}
