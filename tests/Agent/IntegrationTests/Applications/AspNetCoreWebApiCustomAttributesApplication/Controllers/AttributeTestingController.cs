// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace NewRelic.Agent.IntegrationTests.Applications.CustomAttributesWebApi
{
    public class AttributeTestingController : Controller
    {
        [HttpGet]
        [Route("api/CustomAttributes")]
        public string CustomAttributes()
        {
            NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction.AddCustomAttribute("key", "value");
            NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction.AddCustomAttribute("foo", "bar");

            return "success";
        }

        [HttpGet]
        [Route("api/CustomErrorAttributes")]
        public string CustomErrorAttributes()
        {
            var errorAttributes = new Dictionary<string, string>
            {
                {"hey", "dude"},
                {"faz", "baz"},
            };
            NewRelic.Api.Agent.NewRelic.NoticeError("Error occurred.", errorAttributes);

            return "success";
        }

        [HttpGet]
        [Route("api/IgnoreTransaction")]
        public string IgnoreTransaction()
        {
            NewRelic.Api.Agent.NewRelic.IgnoreTransaction();

            return "success";
        }

        [HttpGet]
        [Route("api/CustomAttributesKeyNull")]
        public string CustomAttributesKeyNull()
        {
            NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction.AddCustomAttribute(null, "valuewithnullkey");
            return "success";
        }

        [HttpGet]
        [Route("api/CustomAttributesValueNull")]
        public string CustomAttributesValueNull()
        {
            NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction.AddCustomAttribute("keywithnullvalue", null);
            return "success";
        }
    }
}
