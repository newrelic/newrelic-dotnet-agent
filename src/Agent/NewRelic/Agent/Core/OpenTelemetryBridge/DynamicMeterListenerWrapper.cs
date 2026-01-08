// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.OpenTelemetryBridge
{
    /// <summary>
    /// Implementation of IMeterListenerWrapper that dynamically interacts with the MeterListener type
    /// from the application's DiagnosticSource assembly.
    /// </summary>
    public class DynamicMeterListenerWrapper : IMeterListenerWrapper
    {
        private readonly IAssemblyProvider _assemblyProvider;
        private volatile bool _disposed;
        private object _meterListener;
        private Type _meterListenerType;
        private bool _isAvailable;

        private MethodInfo _startMethod;
        private MethodInfo _enableMeasurementEventsMethod;
        private MethodInfo _recordObservableInstrumentsMethod;
        private MethodInfo _setMeasurementEventCallbackMethod;
        private Action _recordObservableInstrumentsDelegate;

        public Action<object, IMeterListenerWrapper> InstrumentPublished { get; set; }

        // Note: We use object for the listener parameter in internal callbacks to avoid type identity issues with different MeterListener versions.
        public Action<object, object, IMeterListenerWrapper> MeasurementsCompleted { get; set; }

        public DynamicMeterListenerWrapper(IAssemblyProvider assemblyProvider)
        {
            _assemblyProvider = assemblyProvider;
            TryInitialize();
        }

        private void TryInitialize()
        {
            try
            {
                Assembly assembly = null;
                
                // First, try to find an already-loaded assembly
                var assemblies = _assemblyProvider.GetAssemblies()
                    .Where(a => a.GetName().Name == "System.Diagnostics.DiagnosticSource")
                    .ToList();

                if (assemblies.Count > 0)
                {
                    assembly = assemblies.OrderByDescending(a => a.GetName().Version).First();
                }
                else
                {
                    // If not loaded yet, try to load it explicitly
                    // This is common when the agent initializes before application code references System.Diagnostics.Metrics
                    try
                    {
                        assembly = Assembly.Load("System.Diagnostics.DiagnosticSource");
                        Log.Debug("Loaded System.Diagnostics.DiagnosticSource assembly explicitly.");
                    }
                    catch (Exception loadEx)
                    {
                        Log.Debug($"System.Diagnostics.DiagnosticSource assembly not found and could not be loaded: {loadEx.Message}. MeterListener functionality will be unavailable.");
                        _isAvailable = false;
                        return;
                    }
                }

                Log.Debug($"Initializing MeterListener from System.Diagnostics.DiagnosticSource version {assembly.GetName().Version}");

                _meterListenerType = assembly.GetType("System.Diagnostics.Metrics.MeterListener", throwOnError: true);

                _startMethod = _meterListenerType.GetMethod("Start") ?? throw new MissingMethodException(_meterListenerType.Name, "Start");
                _enableMeasurementEventsMethod = _meterListenerType.GetMethod("EnableMeasurementEvents") ?? throw new MissingMethodException(_meterListenerType.Name, "EnableMeasurementEvents");
                _recordObservableInstrumentsMethod = _meterListenerType.GetMethod("RecordObservableInstruments") ?? throw new MissingMethodException(_meterListenerType.Name, "RecordObservableInstruments");
                _setMeasurementEventCallbackMethod = _meterListenerType.GetMethod("SetMeasurementEventCallback") ?? throw new MissingMethodException(_meterListenerType.Name, "SetMeasurementEventCallback");

                _meterListener = Activator.CreateInstance(_meterListenerType);
                
                // Compile RecordObservableInstruments for better performance (called frequently)
                var instanceConstant = Expression.Constant(_meterListener);
                var instanceCast = Expression.Convert(instanceConstant, _meterListenerType);
                var callExpr = Expression.Call(instanceCast, _recordObservableInstrumentsMethod);
                var lambda = Expression.Lambda<Action>(callExpr);
                _recordObservableInstrumentsDelegate = lambda.Compile();
                
                _isAvailable = true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize MeterListener via reflection. OpenTelemetry metrics bridging will be unavailable.");
                _isAvailable = false;
            }
        }

        public void Start()
        {
            if (CheckDisposed() || !EnsureInitialized() || _startMethod == null) return;
            
            try
            {
                ConfigureCallbacks();
                _startMethod.Invoke(_meterListener, null);
            }
            catch (Exception ex)
            {
                Log.Debug($"Failed to start MeterListener: {ex.Message}");
            }
        }

        public void RecordObservableInstruments()
        {
            if (CheckDisposed() || !EnsureInitialized() || _recordObservableInstrumentsDelegate == null) return;
            
            try
            {
                _recordObservableInstrumentsDelegate();
            }
            catch (Exception ex)
            {
                Log.Debug($"Failed to record observable instruments: {ex.Message}");
            }
        }

        private bool EnsureInitialized()
        {
            if (!_isAvailable)
            {
                TryInitialize();
            }
            return _isAvailable;
        }

        private bool CheckDisposed()
        {
            return _disposed;
        }

        public void EnableMeasurementEvents(object instrument, object state)
        {
            if (CheckDisposed() || !_isAvailable || _enableMeasurementEventsMethod == null) return;
            
            try
            {
                _enableMeasurementEventsMethod.Invoke(_meterListener, new[] { instrument, state });
            }
            catch (Exception ex)
            {
                Log.Debug($"Failed to enable measurement events: {ex.Message}");
            }
        }

        public void RegisterMeasurementCallback<T>(MeasurementCallbackDelegate<T> callback) where T : struct
        {
            SetMeasurementCallback(callback);
        }

        public void SetMeasurementCallback<T>(MeasurementCallbackDelegate<T> callback) where T : struct
        {
            if (CheckDisposed() || !_isAvailable || _setMeasurementEventCallbackMethod == null) return;
            
            var setCallbackGeneric = _setMeasurementEventCallbackMethod.MakeGenericMethod(typeof(T));
            var callbackDelegateType = setCallbackGeneric.GetParameters()[0].ParameterType;
            var invokeMethod = callbackDelegateType.GetMethod("Invoke");
            var paramsInfo = invokeMethod.GetParameters();

            var instrumentParam = Expression.Parameter(paramsInfo[0].ParameterType, "instrument");
            var measurementParam = Expression.Parameter(paramsInfo[1].ParameterType, "measurement");
            var tagsParam = Expression.Parameter(paramsInfo[2].ParameterType, "tags");
            var stateParam = Expression.Parameter(paramsInfo[3].ParameterType, "state");

            // Check if tags parameter is what we expect. If not (common on .NET Framework with version mismatches),
            // we use a helper to bridge the span via reflection.
            var ourSpanType = typeof(ReadOnlySpan<KeyValuePair<string, object>>);
            Expression finalCallExpr;

            if (paramsInfo[2].ParameterType == ourSpanType)
            {
                var callbackInvokeMethod = callback.GetType().GetMethod("Invoke");
                finalCallExpr = Expression.Call(Expression.Constant(callback), callbackInvokeMethod,
                    instrumentParam,
                    measurementParam,
                    tagsParam,
                    stateParam);
            }
            else
            {
                // Bridge via helper method to handle different ReadOnlySpan type identity
                var bridgeMethod = typeof(DynamicMeterListenerWrapper).GetMethod(nameof(HandleMeasurementCallbackWithSpan), BindingFlags.NonPublic | BindingFlags.Instance)
                    .MakeGenericMethod(typeof(T), paramsInfo[2].ParameterType);

                finalCallExpr = Expression.Call(Expression.Constant(this), bridgeMethod,
                    Expression.Convert(instrumentParam, typeof(object)),
                    measurementParam,
                    tagsParam,
                    stateParam,
                    Expression.Constant(callback));
            }

            var lambda = Expression.Lambda(callbackDelegateType, finalCallExpr, instrumentParam, measurementParam, tagsParam, stateParam);
            var compiledDelegate = lambda.Compile();
            
            setCallbackGeneric.Invoke(_meterListener, new object[] { compiledDelegate });
        }

        private void HandleMeasurementCallbackWithSpan<T, TSpan>(object instrument, T measurement, TSpan tagsSpan, object state, MeasurementCallbackDelegate<T> callback)
            where T : struct
        {
            try
            {
                var spanType = typeof(TSpan);
                var lengthProp = spanType.GetProperty("Length");
                var getItemMethod = spanType.GetMethod("get_Item", new[] { typeof(int) });

                if (lengthProp == null || getItemMethod == null)
                {
                    callback(instrument, measurement, Span<KeyValuePair<string, object>>.Empty, state);
                    return;
                }

                int length = (int)lengthProp.GetValue(tagsSpan);
                if (length == 0)
                {
                    callback(instrument, measurement, Span<KeyValuePair<string, object>>.Empty, state);
                    return;
                }

                var tags = new KeyValuePair<string, object>[length];
                for (int i = 0; i < length; i++)
                {
                    var kvp = getItemMethod.Invoke(tagsSpan, new object[] { i });
                    if (kvp == null) continue;

                    var kvpType = kvp.GetType();
                    var key = kvpType.GetProperty("Key")?.GetValue(kvp) as string;
                    var value = kvpType.GetProperty("Value")?.GetValue(kvp);
                    tags[i] = new KeyValuePair<string, object>(key, value);
                }

                callback(instrument, measurement, new ReadOnlySpan<KeyValuePair<string, object>>(tags), state);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error bridging measurement callback tags.");
            }
        }

        private void ConfigureCallbacks()
        {
            ConfigureInstrumentPublished();
            ConfigureMeasurementsCompleted();
        }

        private void ConfigureInstrumentPublished()
        {
            var prop = _meterListenerType.GetProperty("InstrumentPublished");
            if (prop == null) 
            {
                Log.Warn("InstrumentPublished property not found on MeterListener type.");
                return;
            }

            var paramsInfo = prop.PropertyType.GetMethod("Invoke").GetParameters();
            var instrumentParam = Expression.Parameter(paramsInfo[0].ParameterType, "instrument");
            var listenerParam = Expression.Parameter(paramsInfo[1].ParameterType, "listener");

            var onPublishedMethod = typeof(DynamicMeterListenerWrapper).GetMethod(nameof(OnInstrumentPublishedInternal), BindingFlags.NonPublic | BindingFlags.Instance);
            var callExpr = Expression.Call(Expression.Constant(this), onPublishedMethod, 
                Expression.Convert(instrumentParam, typeof(object)), 
                Expression.Convert(listenerParam, typeof(object)));

            var lambda = Expression.Lambda(prop.PropertyType, callExpr, instrumentParam, listenerParam);
            prop.SetValue(_meterListener, lambda.Compile());
        }

        private void ConfigureMeasurementsCompleted()
        {
            var prop = _meterListenerType.GetProperty("MeasurementsCompleted");
            if (prop == null)
            {
                Log.Warn("MeasurementsCompleted property not found on MeterListener type.");
                return;
            }

            var paramsInfo = prop.PropertyType.GetMethod("Invoke").GetParameters();
            var instrumentParam = Expression.Parameter(paramsInfo[0].ParameterType, "instrument");
            var stateParam = Expression.Parameter(paramsInfo[1].ParameterType, "state");
            var listenerParam = paramsInfo.Length > 2 ? Expression.Parameter(paramsInfo[2].ParameterType, "listener") : null;

            var onCompletedMethod = typeof(DynamicMeterListenerWrapper).GetMethod(nameof(OnMeasurementsCompletedInternal), BindingFlags.NonPublic | BindingFlags.Instance);
            
            Expression callExpr;
            if (listenerParam != null)
            {
                callExpr = Expression.Call(Expression.Constant(this), onCompletedMethod,
                    Expression.Convert(instrumentParam, typeof(object)),
                    stateParam,
                    Expression.Convert(listenerParam, typeof(object)));
                
                var lambda = Expression.Lambda(prop.PropertyType, callExpr, instrumentParam, stateParam, listenerParam);
                prop.SetValue(_meterListener, lambda.Compile());
            }
            else
            {
                callExpr = Expression.Call(Expression.Constant(this), onCompletedMethod,
                    Expression.Convert(instrumentParam, typeof(object)),
                    stateParam,
                    Expression.Constant(null, typeof(object)));

                var lambda = Expression.Lambda(prop.PropertyType, callExpr, instrumentParam, stateParam);
                prop.SetValue(_meterListener, lambda.Compile());
            }
        }

        private void OnInstrumentPublishedInternal(object instrument, object listener)
        {
            InstrumentPublished?.Invoke(instrument, this);
        }

        private void OnMeasurementsCompletedInternal(object instrument, object state, object listener)
        {
            MeasurementsCompleted?.Invoke(instrument, state, this);
        }

        public void Dispose()
        {
            if (CheckDisposed()) return;

            lock (this)
            {
                if (CheckDisposed()) return;

                _disposed = true;
                (_meterListener as IDisposable)?.Dispose();
            }
        }
    }
}
