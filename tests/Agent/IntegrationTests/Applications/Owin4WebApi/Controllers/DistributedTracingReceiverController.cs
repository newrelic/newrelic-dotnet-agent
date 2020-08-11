// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Web.Http;

namespace Owin4WebApi.Controllers
{
    public class DistributedTracingReceiverController : ApiController
    {
        [HttpGet]
        [Route("api/CallEnd")]
        public string CallEnd()
        {
            return "Worked";
        }
    }
}
