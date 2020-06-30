/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Web.Mvc;

namespace RejitMvcApplication.Controllers
{
    /// <summary>
    /// Endpoints used for Rejit Integration tests.
    /// </summary>
    public class RejitController : Controller
    {
        /// <summary>
        /// HTTP GET method that has no additional methods to instrument.
        /// </summary>
        /// <returns>Returns a string containing: It am working</returns>
        [HttpGet]
        public string Get()
        {
            return "It am working";
        }

        /// <summary>
        /// HTTP GET method that calls CustomMethodDefaultWrapperAddNodeX so that custom instrumentation can be used.
        /// </summary>
        /// <param name="id">Selects which instrumented method to call within the action. Options: 0, 1</param>
        /// <returns>Returns a string containing: It am working</returns>
        [HttpGet]
        public string GetAddNode(int id)
        {
            switch (id)
            {
                case 0:
                    CustomMethodDefaultWrapperAddNode();
                    break;
                case 1:
                    CustomMethodDefaultWrapperAddNode1();
                    break;
                default:
                    CustomMethodDefaultWrapperAddNode();
                    break;
            }

            return "It am working";
        }

        /// <summary>
        /// HTTP GET method that calls CustomMethodDefaultWrapperDeleteNodeX so that custom instrumentation can be used.
        /// </summary>
        /// <param name="id">Selects which instrumented method to call within the action. Options: 0, 1</param>
        /// <returns>Returns a string containing: It am working</returns>
        [HttpGet]
        public string GetDeleteNode(int id)
        {
            switch (id)
            {
                case 0:
                    CustomMethodDefaultWrapperDeleteNode();
                    break;
                case 1:
                    CustomMethodDefaultWrapperDeleteNode1();
                    break;
                default:
                    CustomMethodDefaultWrapperDeleteNode();
                    break;
            }

            return "It am working";
        }

        /// <summary>
        /// HTTP GET method that calls CustomMethodDefaultWrapperAddAttribute so that custom instrumentation can be used.
        /// </summary>
        /// <returns>Returns a string containing: It am working</returns>
        [HttpGet]
        public string GetAddAttribute()
        {
            CustomMethodDefaultWrapperAddAttribute();
            return "It am working";
        }

        /// <summary>
        /// HTTP GET method that calls CustomMethodDefaultWrapperChangeAttribute so that custom instrumentation can be used.
        /// </summary>
        /// <returns>Returns a string containing: It am working</returns>
        [HttpGet]
        public string GetChangeAttributeValue()
        {
            CustomMethodDefaultWrapperChangeAttributeValue();
            return "It am working";
        }

        /// <summary>
        /// HTTP GET method that calls CustomMethodDefaultWrapperDeleteAttribute so that custom instrumentation can be used.
        /// </summary>
        /// <returns>Returns a string containing: It am working</returns>
        [HttpGet]
        public string GetDeleteAttribute()
        {
            CustomMethodDefaultWrapperDeleteAttribute();
            return "It am working";
        }

        /// <summary>
        /// HTTP GET method that calls CustomMethodDefaultWrapperAddFile so that custom instrumentation can be used.
        /// </summary>
        /// <returns>Returns a string containing: It am working</returns>
        [HttpGet]
        public string GetAddFile()
        {
            CustomMethodDefaultWrapperAddFile();
            return "It am working";
        }

        /// <summary>
        /// HTTP GET method that calls CustomMethodDefaultWrapperRenameFile so that custom instrumentation can be used.
        /// </summary>
        /// <returns>Returns a string containing: It am working</returns>
        [HttpGet]
        public string GetRenameFile()
        {
            CustomMethodDefaultWrapperRenameFile();
            return "It am working";
        }

        /// <summary>
        /// HTTP GET method that calls CustomMethodDefaultWrapperDeleteFile so that custom instrumentation can be used.
        /// </summary>
        /// <returns>Returns a string containing: It am working</returns>
        [HttpGet]
        public string GetDeleteFile()
        {
            CustomMethodDefaultWrapperDeleteFile();
            return "It am working";
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CustomMethodDefaultWrapperAddNode()
        {
            Thread.Sleep(TimeSpan.FromMilliseconds(5));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CustomMethodDefaultWrapperAddNode1()
        {
            Thread.Sleep(TimeSpan.FromMilliseconds(5));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CustomMethodDefaultWrapperDeleteNode()
        {
            Thread.Sleep(TimeSpan.FromMilliseconds(5));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CustomMethodDefaultWrapperDeleteNode1()
        {
            Thread.Sleep(TimeSpan.FromMilliseconds(5));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CustomMethodDefaultWrapperAddAttribute()
        {
            Thread.Sleep(TimeSpan.FromMilliseconds(5));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CustomMethodDefaultWrapperChangeAttributeValue()
        {
            Thread.Sleep(TimeSpan.FromMilliseconds(5));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CustomMethodDefaultWrapperDeleteAttribute()
        {
            Thread.Sleep(TimeSpan.FromMilliseconds(5));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CustomMethodDefaultWrapperAddFile()
        {
            Thread.Sleep(TimeSpan.FromMilliseconds(5));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CustomMethodDefaultWrapperRenameFile()
        {
            Thread.Sleep(TimeSpan.FromMilliseconds(5));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CustomMethodDefaultWrapperDeleteFile()
        {
            Thread.Sleep(TimeSpan.FromMilliseconds(5));
        }
    }
}
