// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Threading;
using NewRelic.Agent.Core.Tracer;

namespace NewRelic.Agent.Core
{
    /// <summary>
    /// The AgentShim is called by our injected bytecode.
    /// It exists to provide a layer of protection against deadlocks and such that can occur while the agent is initializing itself early on.
    /// Once the agent is initialized, calls will fall almost straight through to the Agent class.
    /// </summary>
    public class AgentShim
    {
        static void Initialize()
        {
            AgentInitializer.InitializeAgent();
        }

#if NETSTANDARD2_0
		static AgentShim()
		{
			Initialize();
		}
#else
        private static bool _initialized = false;
        private static object _initLock = new object();

        static bool TryInitialize(string method)
        {
            if (Monitor.IsEntered(_initLock)) return false;
            if (DeferInitialization(method)) return false;

            lock (_initLock)
            {
                if (!_initialized)
                {
                    Initialize();
                    _initialized = true;
                }

                return true;
            }
        }

        private static HashSet<string> _deferInitializationOnTheseMethods = new HashSet<string>
        {
            "System.Net.Http.HttpClient.SendAsync",
            "System.Net.HttpWebRequest.SerializeHeaders",
            "System.Net.HttpWebRequest.GetResponse"
        };

        private static HashSet<string> DeferInitializationOnTheseMethods
        {
            get
            {
                if (!_deferAgentInitMethodListInitialized)
                {
                    InitializeDeferAgentInitMethodList();
                }

                return _deferInitializationOnTheseMethods;
            }
        }

        private static bool _deferAgentInitMethodListInitialized = false;
        private static object _methodListInitLock = new object();

        private static void InitializeDeferAgentInitMethodList()
        {
            var methodsFromEnvVar = System.Environment.GetEnvironmentVariable("NEW_RELIC_DELAY_AGENT_INIT_METHOD_LIST");
            var additionalMethods = !string.IsNullOrWhiteSpace(methodsFromEnvVar)
                ? methodsFromEnvVar.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                : new string[] { };

            lock (_methodListInitLock)
            {
                if (!_deferAgentInitMethodListInitialized)
                {
                    foreach (var method in additionalMethods)
                    {
                        _deferInitializationOnTheseMethods.Add(method);
                    }
                    _deferAgentInitMethodListInitialized = true;
                }
            }
        }

        static bool DeferInitialization(string method)
        {
            return DeferInitializationOnTheseMethods.Contains(method);
        }
#endif

        /// <summary>
        /// Creates a tracer (if appropriate) and returns a delegate for the tracer's finish method.
        /// This method is reflectively invoked from the injected bytecode if the CLR is greater than 2.0.
        /// </summary>
        public static Action<object, Exception> GetFinishTracerDelegate(
            string tracerFactoryName,
            uint tracerArguments,
            string metricName,
            string assemblyName,
            Type type,
            string typeName,
            string methodName,
            string argumentSignature,
            object invocationTarget,
            object[] args,
            ulong functionId)
        {
#if NETFRAMEWORK
			if (!_initialized)
			{
				if (!TryInitialize($"{typeName}.{methodName}")) return NoOpFinishTracer;
			}
#endif

            var tracer = GetTracer(
                tracerFactoryName,
                tracerArguments,
                metricName,
                assemblyName,
                type,
                typeName,
                methodName,
                argumentSignature,
                invocationTarget,
                args,
                functionId);

            if (tracer == null)
            {
                return NoOpFinishTracer;
            }
            else
            {
                return new TracerWrapper(tracer).FinishTracer;
            }
        }

        /// <summary>
        /// Returns a tracer. This method is reflectively invoked from the injected bytecode if the CLR is 2.0 (which
        /// does not include System.Action).  In that CLR the injected code will reflectively invoke the static FinishTracer
        /// method at the end of the method.
        /// Changing the signature of this method will break the C++ code and the way that it generates called-functions' signatures.
        /// </summary>
        /// <param name="tracerFactoryName">The fully qualified name of the TracerFactory,
        /// from the mapping held in the CoreInstrumentation.xml file,
        /// as set up by unmanaged the C++ profiler code.</param>
        /// <param name="tracerArguments">A packed value with items from the instrumentation .xml files</param>
        /// <param name="metricName"></param>
        /// <param name="assemblyName"></param>
        /// <param name="type"></param>
        /// <param name="typeName"></param>
        /// <param name="methodName"></param>
        /// <param name="argumentSignature"></param>
        /// <param name="invocationTarget"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        /// <returns>Returns an Action<object, Exception> delegate which invokes ITracer.Finish.  
        /// We can directly invoke this delegate instead of using reflection.  Null should never be returned.</returns>
        /// <exception cref="System.ArgumentNullException"> thrown if any one of <paramref name="assemblyName"/>, <paramref name="type"/>,
        /// <paramref name="typeName"/>, <paramref name="methodName"/>, <paramref name="argumentSignature"/> or <paramref name="args"/> is null. 
        /// This function is only called from the injected managed byte-code</exception>
        public static ITracer GetTracer(
            string tracerFactoryName,
            uint tracerArguments,
            string metricName,
            string assemblyName,
            Type type,
            string typeName,
            string methodName,
            string argumentSignature,
            object invocationTarget,
            object[] args,
            ulong functionId)
        {
            try
            {
                if (type == null)
                    throw new ArgumentNullException("type");
                if (assemblyName == null)
                    throw new ArgumentNullException("assemblyName");
                if (typeName == null)
                    throw new ArgumentNullException("typeName");
                if (methodName == null)
                    throw new ArgumentNullException("methodName");
                if (argumentSignature == null)
                    throw new ArgumentNullException("argumentSignature");
                if (args == null)
                    throw new ArgumentNullException("args");

                if (IgnoreWork.AgentDepth > 0)
                    return null;

                var agent = AgentManager.Instance;

                return agent?.GetTracerImpl(
                    tracerFactoryName,
                    tracerArguments,
                    metricName,
                    assemblyName,
                    type,
                    typeName,
                    methodName,
                    argumentSignature,
                    invocationTarget,
                    args,
                    functionId);
            }

            // http://msdn.microsoft.com/en-us/library/system.threading.threadabortexception.aspx
            // ThreadAbortException is a special exception that can be caught,
            // but it will automatically be raised again at the end of the catch block.
            // We don't want our Exception handler to catch this exception,
            // since it will throw anyway with a different exception stack.
            catch (ThreadAbortException)
            {
                throw;
            }
            catch (Exception exception)
            {
                try
                {
                    Serilog.Log.Logger.Debug(exception, "Exception occurred in AgentShim.GetTracer");
                }
                catch
                {
                    // failure to log the exception will unfortunately be swallowed, better than crashing the user's application!
                }

                return null;
            }
        }

        /// <summary>
        /// Returns a tracer.
        /// This method is called from the injected bytecode.  Changing the signature of this method will break things.
        /// Calls Finish(returnValue, exceptionObject) on the given tracer object.
        /// If any exception is thrown by the New Relic code, then it is caught and logged.
        /// The injected bytecode doesn't directly call the tracer's finish method
        /// because the .NET 4.0 security model will throw a VerificationException.
        /// </summary>
        /// <param name="tracerObject"></param>
        /// <param name="returnValue"></param>
        /// <param name="exceptionObject"></param>
        public static void FinishTracer(object tracerObject, object returnValue, object exceptionObject)
        {
            try
            {
                // no tracer means no finish call
                if (null == tracerObject)
                    return;

                // validate the tracer we received from the injected code
                ITracer tracer = tracerObject as ITracer;
                if (tracer == null)
                {
                    Serilog.Log.Logger.Error($"AgentShim.FinishTracer received a tracer object but it was not an ITracer. {tracerObject}");
                    return;
                }

                // validate the exception we received from the injected code
                Exception exception = exceptionObject as Exception;
                if (exception == null && exceptionObject != null)
                {
                    Serilog.Log.Logger.Error($"AgentShim.FinishTracer received an exception object but it was not an Exception. {exceptionObject}");
                    return;
                }

                tracer.Finish(returnValue, exception);
            }

            // http://msdn.microsoft.com/en-us/library/system.threading.threadabortexception.aspx
            // ThreadAbortException is a special exception that can be caught,
            // but it will automatically be raised again at the end of the catch block.
            // We don't want our Exception handler to catch this exception,
            // since it will throw anyway with a different exception stack.
            catch (ThreadAbortException)
            {
                throw;
            }
            catch (Exception exception)
            {
                try
                {
                    Serilog.Log.Logger.Debug(exception,"Exception occurred in AgentShim.FinishTracer");
                }
                catch
                {
                    // if logging fails we have to suck it up and swallow the exception
                }
            }
        }

        /// <summary>
        /// Returned as an System.Action delegate from calls to GetTracer when the tracer is null or an error occurred.
        /// </summary>
        private static void NoOpFinishTracer(object value, Exception exception)
        {
        }
    }

    /// <summary>
    /// Wraps a tracer in order to invoke the static AgentShim.FinishTracer method.  Later we can refactor the 
    /// static FinishTracer behavior into a base class implementation of ITracer and do away with this wrapper.
    /// </summary>
    public class TracerWrapper
    {
        private readonly ITracer tracer;
        public TracerWrapper(ITracer tracer)
        {
            this.tracer = tracer;
        }

        public void FinishTracer(object returnValue, Exception exception)
        {
            AgentShim.FinishTracer(tracer, returnValue, exception);
        }
    }

    /// <summary>
    /// Used to stop re-entry into the agent via AgentShim entry points when the call stack contains agent code already.
    /// The profiler stops reentry of GetTracer/FinishTracer twice in the same call stack, but it doesn't stop the agent from spinning up a background thread and then entering the agent from that thread.  To resolve this, anytime a new thread is spun up (via any mechanism including async, Timer, Thread, ThreadPool, etc.) the work inside it needs to be wrapped in using (new IgnoreWork()) as a way of telling AgentShim to not re-enter.
    /// </summary>
    public class IgnoreWork : IDisposable
    {
        [ThreadStatic]
        public static uint AgentDepth = 0;

        public IgnoreWork()
        {
            ++AgentDepth;
        }

        public void Dispose()
        {
            --AgentDepth;
        }
    }
}
