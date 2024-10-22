// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq.Expressions;
using System.Reflection;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Reflection;

namespace NewRelic.Agent.Core.Samplers
{
    // to allow for unit testing
    public interface IGCSamplerV2ReflectionHelper
    {
        Func<object, object> GetGenerationInfo { get; }
        bool ReflectionFailed { get; }
        Func<object, object> GCGetMemoryInfo_Invoker { get; }
        Func<object, object> GCGetTotalAllocatedBytes_Invoker { get; }
    }

    public class GCSamplerV2ReflectionHelper : IGCSamplerV2ReflectionHelper
    {
        public Func<object, object> GetGenerationInfo { get; private set; }
        public bool ReflectionFailed { get; private set; }
        public Func<object, object> GCGetMemoryInfo_Invoker { get; private set; }
        public Func<object, object> GCGetTotalAllocatedBytes_Invoker { get; private set; }

        public GCSamplerV2ReflectionHelper()
        {
            try
            {
                var assembly = Assembly.Load("System.Runtime");
                var gcType = assembly.GetType("System.GC");
                var paramType = assembly.GetType("System.GCKind");
                var returnType = assembly.GetType("System.GCMemoryInfo");

                if (!VisibilityBypasser.Instance.TryGenerateOneParameterStaticMethodCaller(gcType, "GetGCMemoryInfo", paramType, returnType, out var accessor))
                {
                    ReflectionFailed = true;
                }
                else
                    GCGetMemoryInfo_Invoker = accessor;

                if (!ReflectionFailed)
                {
                    paramType = assembly.GetType("System.Boolean");
                    returnType = assembly.GetType("System.Int64");
                    if (!VisibilityBypasser.Instance.TryGenerateOneParameterStaticMethodCaller(gcType, "GetTotalAllocatedBytes", paramType, returnType, out var accessor1))
                    {
                        ReflectionFailed = true;
                    }
                    else
                        GCGetTotalAllocatedBytes_Invoker = accessor1;
                }

                if (!ReflectionFailed)
                    GetGenerationInfo = GCMemoryInfoHelper.GenerateGetMemoryInfoMethod();
            }
            catch (Exception e)
            {
                Log.Warn(e, $"Failed to initialize GCSamplerV2ReflectionHelper.");
                ReflectionFailed = true;
            }
        }
    }

    internal static class GCMemoryInfoHelper
    {
        /// <summary>
        /// Generate a function that takes a GCMemoryInfo instance as an input parameter and 
        /// returns an array of GCGenerationInfo instances.
        ///
        /// Essentially builds the equivalent of
        ///    object Foo(object input) => ((GCMemoryInfo)input).GenerationInfo.ToArray();
        /// </summary>
        public static Func<object, object> GenerateGetMemoryInfoMethod()
        {
            var assembly = Assembly.Load("System.Runtime");
            var gcMemoryInfoType = assembly.GetType("System.GCMemoryInfo");

            // Define a parameter expression for the input object
            var inputParameter = Expression.Parameter(typeof(object), "input");

            // Cast the input parameter to GCMemoryInfo
            var gcMemoryInfoParameter = Expression.Convert(inputParameter, gcMemoryInfoType);

            // Get the GenerationInfo property
            var generationInfoProperty = gcMemoryInfoType.GetProperty("GenerationInfo");

            // Access the GenerationInfo property
            var accessGenerationInfo = Expression.Property(gcMemoryInfoParameter, generationInfoProperty);

            // Get the ReadOnlySpan<GCGenerationInfo> type using the full type name
            var readOnlySpanType = assembly.GetType("System.ReadOnlySpan`1[[System.GCGenerationInfo, System.Private.CoreLib]]");

            // Get the ToArray method of ReadOnlySpan<GCGenerationInfo>
            var toArrayMethod = readOnlySpanType.GetMethod("ToArray", BindingFlags.Public | BindingFlags.Instance);

            // Call ToArray() on GenerationInfo
            var callToArray = Expression.Call(accessGenerationInfo, toArrayMethod);

            // Create a lambda expression
            var lambda = Expression.Lambda<Func<object, object>>(Expression.Convert(callToArray, typeof(object)), inputParameter);

            // Compile the lambda expression into a delegate
            return lambda.Compile();
        }
    }
}
