/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace AspNetCoreMvcFrameworkAsyncApplication.Controllers
{
    public class ManualAsyncController : Controller
    {
        public string TaskRunBlocked()
        {
            var task = Task.Run(() => TaskRunBackgroundMethod());

            return task.Result;
        }

        public string TaskFactoryStartNewBlocked()
        {
            Task.Factory.StartNew(TaskFactoryStartNewBackgroundMethod).Wait();

            return "Worked";
        }

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
