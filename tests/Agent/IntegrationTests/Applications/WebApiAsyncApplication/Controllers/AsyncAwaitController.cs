/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Web.Http;

namespace WebApiAsyncApplication.Controllers
{
    public class AsyncAwaitController : ApiController
    {
        [HttpGet]
        [Route("AsyncAwait/IoBoundNoSpecialAsync")]
        public async Task<string> IoBoundNoSpecialAsync()
        {
            var async1 = CustomMethodAsync1();
            var async2 = CustomMethodAsync2();

            await Task.WhenAll(async1, async2);

            await CustomMethodAsync3();

            return "Worked";
        }

        [HttpGet]
        [Route("AsyncAwait/IoBoundConfigureAwaitFalseAsync")]
        public async Task<string> IoBoundConfigureAwaitFalseAsync()
        {
            await ConfigureAwaitFalseExampleAsync();

            return "Worked";
        }

        [HttpGet]
        [Route("AsyncAwait/CpuBoundTasksAsync")]
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        private async Task ConfigureAwaitSubMethodAsync2()
        {
            await Task.Delay(1);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private async Task CustomMethodAsync3()
        {
            await Task.Delay(1);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task CustomMethodAsync2()
        {
            await Task.Delay(1);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task CustomMethodAsync1()
        {
            await Task.Delay(1);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void TaskFactoryStartNewBackgroundMethod()
        {
            //do nothing
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void TaskRunBackgroundMethod()
        {
            // Force transaction trace on the route which calls this method
            System.Threading.Thread.Sleep(5000);
        }

    }
}
