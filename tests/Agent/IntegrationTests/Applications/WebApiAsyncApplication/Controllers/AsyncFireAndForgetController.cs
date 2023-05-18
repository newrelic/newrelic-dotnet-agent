// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Api.Agent;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace WebApiAsyncApplication.Controllers
{
    public class AsyncFireAndForgetController : ApiController
    {
        private const int DELAY_TIME = 2000;
        private const int SHORT_DELAY_TIME = 200;

        [HttpGet]
        [Route("AsyncFireAndForget/Async_AwaitedAsync")]
        public async Task<string> Async_AwaitedAsync()
        {
            var transactionName = UpdateTransactionName("AA");

            var t = Task.Run(() => AsyncMethod(DELAY_TIME, transactionName));
            //var t = AsyncMethod(DELAY_TIME, transactionName);

            Thread.Sleep(SHORT_DELAY_TIME);

            await t;

            transactionName = UpdateTransactionName(transactionName, "AA");

            //WITH XML INSTRUMENTATION
            //AA-AA                 -Web Transaction
            //AA-AM-AM              -Other Transaction

            //WITHOUT
            //AA-AA                 -Web transaction

            return "Worked";
        }

        [HttpGet]
        [Route("AsyncFireAndForget/Async_FireAndForget")]
        public Task<string> Async_FireAndForget()
        {
            var transactionName = UpdateTransactionName("AF");

#pragma warning disable CS4014
            Task.Run(() => AsyncMethod(DELAY_TIME, transactionName));
#pragma warning restore CS4014

            Thread.Sleep(SHORT_DELAY_TIME);

            transactionName = UpdateTransactionName(transactionName, "AF");

            //WITH XML INSTRUMENTATION
            //  AF-AF                       Web
            //  AF-AM-AM                    Other

            //WITHOUT
            //  AF-* Not sure, probably an error

            return Task.FromResult("Worked");
        }

        [HttpGet]
        [Route("AsyncFireAndForget/Async_Sync")]
        public Task<string> Async_Sync()
        {
            var transactionName = UpdateTransactionName("AS");

            SyncMethod(DELAY_TIME, transactionName);

            Thread.Sleep(SHORT_DELAY_TIME);

            transactionName = UpdateTransactionName(transactionName, "AS");

            // WITH XML INSTRUMENTATION
            //  AS-AS

            // WITHOUT
            //  AS-AS

            return Task.FromResult("Worked");
        }

        [HttpGet]
        [Route("AsyncFireAndForget/Sync_AwaitedAsync")]
        public string Sync_AwaitedAsync()
        {
            var transactionName = UpdateTransactionName("SA");

            var t = Task.Run(() => AsyncMethod(DELAY_TIME, transactionName));

            Thread.Sleep(SHORT_DELAY_TIME);

            t.Wait();

            transactionName = UpdateTransactionName(transactionName, "SA");

            // WITH XML INSTRUMENTATION
            //    SA-SA
            //    SA-AM-AM

            // WITHOUT
            //        SA-SA

            return "Worked";
        }

        [HttpGet]
        [Route("AsyncFireAndForget/Sync_FireAndForget")]
        public string Sync_FireAndForget()
        {
            var transactionName = UpdateTransactionName("SF");

            var t = Task.Run(() => AsyncMethod(DELAY_TIME, transactionName));

            Thread.Sleep(SHORT_DELAY_TIME);

            transactionName = UpdateTransactionName(transactionName, "SF");

            // WITH XML INSTRUMENTATION
            //  SF-SF
            //  SF-AM-AM
            // WITHOUT
            //  SF - not sure - probably an error

            return "Worked";
        }

        [HttpGet]
        [Route("AsyncFireAndForget/Sync_Sync")]
        public string Sync_Sync()
        {
            var transactionName = UpdateTransactionName("SS");

            SyncMethod(DELAY_TIME, transactionName);

            Thread.Sleep(SHORT_DELAY_TIME);

            transactionName = UpdateTransactionName(transactionName, "SS");

            // WITH XML INSTRUMENTATION
            //  SS - SS

            // WITHOUT
            //  SS - SS

            return "Worked";
        }


        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Task AsyncMethod(int delayMs, string transactionName)
        {
            transactionName = UpdateTransactionName(transactionName, "AM");

            Thread.Sleep(delayMs);

            transactionName = UpdateTransactionName(transactionName, "AM");

            return Task.CompletedTask;
        }


        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SyncMethod(int delayMs, string transactionName)
        {
            transactionName = UpdateTransactionName(transactionName, "SM");

            Thread.Sleep(delayMs);

            transactionName = UpdateTransactionName(transactionName, "SM");

        }


        /// <summary>
        /// Helps build up the transaction name as a mini trace of what occurred.
        /// The transaction name is built up to make sure that the correct storage context
        /// is used to hold the transaction.  One way of doing this is by manipulating the transaction
        /// name and exploring it later.
        /// </summary>
        /// <param name="existingName"></param>
        /// <param name="token"></param>
        /// <param name="isStart"></param>
        /// <returns></returns>
        private static string UpdateTransactionName(string existingName, string token)
        {
            var newName = existingName + "-" + token;

            NewRelic.Api.Agent.NewRelic.SetTransactionName("FireAndForgetTests", newName);

            return newName;
        }

        private static string UpdateTransactionName(string token)
        {
            NewRelic.Api.Agent.NewRelic.SetTransactionName("FireAndForgetTests", token);

            return token;
        }
    }
}
