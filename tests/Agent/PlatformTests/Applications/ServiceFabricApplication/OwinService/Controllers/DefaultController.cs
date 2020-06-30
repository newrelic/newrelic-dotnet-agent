/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System.Web.Http;

namespace OwinService.Controllers
{
    public class DefaultController : ApiController
    {
        [HttpGet]
        [Route("")]
        public string Index()
        {
            return "Hello World!";
        }
    }
}
