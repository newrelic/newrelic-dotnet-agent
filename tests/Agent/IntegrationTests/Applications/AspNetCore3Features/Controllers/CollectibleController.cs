/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using AspNetCore3Features.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace AspNetCore3Features.Controllers
{
    public class CollectibleController : Controller
    {
        public class CollectibleAssemblyLoadContext : AssemblyLoadContext
        {
            public CollectibleAssemblyLoadContext() : base(isCollectible: true)
            { }

            protected override Assembly Load(AssemblyName assemblyName) => null;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        // Create a weak reference to the AssemblyLoadContext that will allow us to detect when the unload completes.
        public static void ExecuteAssembly(out WeakReference alcWeakRef)
        {
            var context = new CollectibleAssemblyLoadContext();
            alcWeakRef = new WeakReference(context);
            {
                var assemblyPath = Path.Combine(Directory.GetCurrentDirectory(), "NetCore3Collectible.dll");
                var assembly = context.LoadFromAssemblyPath(assemblyPath);
                var type = assembly.GetType("NetCore3Collectible.CollectibleInstrumented");
                var instance = Activator.CreateInstance(type);
                var GetDistributedTracePayload = type.GetMethod("GetDistributedTracePayload", BindingFlags.Instance | BindingFlags.Public);
                var result = GetDistributedTracePayload?.Invoke(instance, null); //instance method, no parameters
                if (null == result)
                {
                    throw new Exception("NetCore3Collectible.CollectibleInstrumented.GetDistributedTracePayload returned a null");
                }
            }
            context.Unload();
        }



        public IActionResult AccessCollectible()
        {
            int collectCount = 0;
            void CollectAndFinalize()
            {
                collectCount++;
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            ExecuteAssembly(out WeakReference hostAlcWeakRef);

            do
            {
                CollectAndFinalize();
            }
            while (hostAlcWeakRef.IsAlive && collectCount < 10);

            Console.WriteLine($"{nameof(CollectAndFinalize)} was called {collectCount} time(s).");
            return hostAlcWeakRef.IsAlive ? StatusCode(500) : Ok();
        }
    }
}
