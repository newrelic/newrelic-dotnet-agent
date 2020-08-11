// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Api.Agent;
using System;
using System.Threading;
using System.Web.Http;

namespace WebApiAsyncApplication.Controllers
{
    public class ResponseTimeController : ApiController
    {
        [HttpGet]
        [Route("ResponseTime/CallsOtherMethod/{delaySeconds}")]
        public string CallsOtherTrxWrapperMethod(int delaySeconds)
        {
            InstrumentedMethod(delaySeconds);
            Thread.Sleep(TimeSpan.FromSeconds(delaySeconds));

            //Expectation is that the response time is at-least 10-sec
            //Expectation is that the transaction time is > response time
            //No messages are generated in the log file

            return "Worked";
        }

        [Transaction]
        private void InstrumentedMethod(int delaySeconds)
        {
            Thread.Sleep(TimeSpan.FromSeconds(delaySeconds));
        }

    }
}
