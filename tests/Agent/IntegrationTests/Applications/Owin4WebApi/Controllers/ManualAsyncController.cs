// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace Owin4WebApi.Controllers
{
    public class ManualAsyncController : ApiController
    {
        [HttpGet]
        [Route("ManualAsync/TaskRunBlocked")]
        public string TaskRunBlocked()
        {
            var task = Task.Run(() => TaskRunBackgroundMethod());

            return task.Result;
        }

        [HttpGet]
        [Route("ManualAsync/TaskFactoryStartNewBlocked")]
        public string TaskFactoryStartNewBlocked()
        {
            Task.Factory.StartNew(TaskFactoryStartNewBackgroundMethod).Wait();

            return "Worked";
        }

        [HttpGet]
        [Route("ManualAsync/NewThreadStartBlocked")]
        public string NewThreadStartBlocked()
        {
            var thread = new Thread(ThreadStartBackgroundMethod);
            thread.Start();

            thread.Join();

            return "Worked";
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private string TaskRunBackgroundMethod()
        {
            return "Worked";
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void TaskFactoryStartNewBackgroundMethod()
        {
            //do nothing
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ThreadStartBackgroundMethod()
        {
            //do nothing
        }


    }
}
