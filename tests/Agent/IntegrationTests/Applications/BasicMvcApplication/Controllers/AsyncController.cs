/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Web.Mvc;
using NewRelic.Api.Agent;

namespace BasicMvcApplication.Controllers
{
    public class AsyncController : Controller
    {
        public async Task<string> IoBoundConfigureAwaitFalseAsync()
        {
            await ConfigureAwaitFalseExampleAsync();

            return "Worked";
        }

        public async Task<string> CpuBoundTasksAsync()
        {
            await Task.Run(() => TaskRunBackgroundMethod());
            await Task.Factory.StartNew(TaskFactoryStartNewBackgroundMethod);

            return "Worked";
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private async Task ConfigureAwaitFalseExampleAsync()
        {
            //it is important we don't hit any async instrumentation prior to ConfigureAwait(false)
            await Task.Delay(1).ConfigureAwait(false);
            await ConfigureAwaitSubMethodAsync2();
        }

        [Trace]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private async Task ConfigureAwaitSubMethodAsync2()
        {
            await Task.Delay(1);
        }

        [Trace]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void TaskFactoryStartNewBackgroundMethod()
        {
            //do nothing
        }

        [Trace]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void TaskRunBackgroundMethod()
        {
            //do nothing
        }



    }
}
