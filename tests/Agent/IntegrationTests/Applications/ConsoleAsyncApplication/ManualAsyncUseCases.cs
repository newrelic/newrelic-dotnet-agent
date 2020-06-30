/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NewRelic.Api.Agent;

namespace ConsoleAsyncApplication
{
    public class ManualAsyncUseCases
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

        public int MultipleThreadSegmentParenting()
        {
            var task1 = Task.Run(() => Task1());
            var task2 = Task.Run(() => Task2());
            var task3 = Task.Run(() => Task3());

            // Force a transaction trace
            Thread.Sleep(5000);

            return task1.Result + task2.Result + task3.Result;
        }

        [Trace]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private string TaskRunBackgroundMethod()
        {
            return TaskRunBackgroundSubMethod();
        }

        [Trace]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private string TaskRunBackgroundSubMethod()
        {
            return "Worked";
        }


        [Trace]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void TaskFactoryStartNewBackgroundMethod()
        {
            TaskFactoryStartNewBackgroundSubMethod();
        }

        [Trace]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void TaskFactoryStartNewBackgroundSubMethod()
        {
            //do nothing
        }

        [Trace]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ThreadStartBackgroundMethod()
        {
            ThreadStartBackgroundSubMethod();

        }

        [Trace]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ThreadStartBackgroundSubMethod()
        {
            //do nothing
        }

        [Trace]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private int Task1()
        {
            return Task1SubMethod1() + Task1SubMethod2();
        }

        [Trace]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private int Task1SubMethod1()
        {
            return 1;
        }

        [Trace]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private int Task1SubMethod2()
        {
            return 1;
        }

        [Trace]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private int Task2()
        {
            return Task2SubMethod1() + Task2SubMethod2();
        }

        [Trace]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private int Task2SubMethod1()
        {
            return 1;
        }

        [Trace]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private int Task2SubMethod2()
        {
            return 1;
        }

        [Trace]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private int Task3()
        {
            return Task3SubMethod1() + Task3SubMethod2();
        }

        [Trace]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private int Task3SubMethod1()
        {
            return 1;
        }

        [Trace]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private int Task3SubMethod2()
        {
            return 1;
        }
    }
}
