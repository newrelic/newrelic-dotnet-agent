// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace NewRelic.Agent.IntegrationTests.Applications.CustomAttributesWebApi;

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

    [HttpGet]
    [Route("api/CustomArrayAttributes")]
    public string CustomArrayAttributes()
    {
        NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction.AddCustomAttribute("stringArray", new[] { "red", "green", "blue" });
        NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction.AddCustomAttribute("intArray", new[] { 1, 2, 3, 4, 5 });
        NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction.AddCustomAttribute("boolArray", new[] { true, false, true });

        return "success";
    }

    [HttpGet]
    [Route("api/CustomEmptyArrayAttributes")]
    public string CustomEmptyArrayAttributes()
    {
        NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction.AddCustomAttribute("emptyArray", new string[] { });
        NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction.AddCustomAttribute("nullOnlyArray", new object[] { null, null });

        return "success";
    }

    [HttpGet]
    [Route("api/CustomArrayWithNulls")]
    public string CustomArrayWithNulls()
    {
        NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction.AddCustomAttribute("arrayWithNulls", new object[] { "first", null, "third" });
        NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction.AddCustomAttribute("listAttribute", new List<string> { "list1", "list2", "list3" });

        return "success";
    }

    [HttpGet]
    [Route("api/CustomArrayErrorAttributes")]
    public string CustomArrayErrorAttributes()
    {
        var errorAttributes = new Dictionary<string, object>
        {
            {"errorTags", new[] { "error", "critical", "timeout" }},
            {"errorCodes", new[] { 500, 503, 404 }},
        };
        NewRelic.Api.Agent.NewRelic.NoticeError("Array error occurred.", errorAttributes);

        return "success";
    }
}