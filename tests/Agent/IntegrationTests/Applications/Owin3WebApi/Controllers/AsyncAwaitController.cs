// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;


namespace Owin3WebApi.Controllers
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

        [HttpGet]
        [Route("AsyncAwait/CustomMiddlewareIoBoundNoSpecialAsync")]
        public async Task<string> CustomMiddlewareIoBoundNoSpecialAsync()
        {
            return await Task.FromResult("Worked");
        }

#pragma warning disable 1998
        [HttpGet]
        [Route("AsyncAwait/ErrorResponse")]
        public async Task<string> ErrorResponse()
        {
            throw new ArgumentException("oops");
        }
#pragma warning restore 1998

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

        [HttpPost]
        [Route("AsyncAwait/SimplePostAsync")]
        public async Task<string> SimplePostAsync([FromBody] string value)
        {
            return await Task.FromResult(value);
        }

        public override async Task<HttpResponseMessage> ExecuteAsync(HttpControllerContext controllerContext, CancellationToken cancellationToken)
        {
            if (controllerContext == null)
            {
                throw new ArgumentNullException(nameof(controllerContext));
            }

            try
            {
                return await base.ExecuteAsync(controllerContext, cancellationToken);
            }
            catch (ArgumentException)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }
    }
}
