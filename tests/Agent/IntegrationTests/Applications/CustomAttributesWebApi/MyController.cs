using System;
using System.Collections.Generic;
using System.Threading;
using System.Web.Http;

namespace NewRelic.Agent.IntegrationTests.Applications.CustomAttributesWebApi
{
    public class MyController : ApiController
    {
        [HttpGet]
        [Route("api/CustomAttributes")]
        public String CustomAttributes()
        {
            NewRelic.Api.Agent.NewRelic.AddCustomParameter("key", "value");
            NewRelic.Api.Agent.NewRelic.AddCustomParameter("foo", "bar");

            return "success";
        }

        [HttpGet]
        [Route("api/CustomErrorAttributes")]
        public String CustomErrorAttributes()
        {
            var errorAttributes = new Dictionary<String, String>
            {
                {"hey", "dude"},
                {"faz", "baz"},
            };
            NewRelic.Api.Agent.NewRelic.NoticeError("Error occurred.", errorAttributes);

            return "success";
        }

        [HttpGet]
        [Route("api/IgnoreTransaction")]
        public String IgnoreTransaction()
        {
            NewRelic.Api.Agent.NewRelic.IgnoreTransaction();

            return "success";
        }

        [HttpGet]
        [Route("api/CustomAttributesKeyNull")]
        public String CustomAttributesKeyNull()
        {
            NewRelic.Api.Agent.NewRelic.AddCustomParameter(null, "valuewithnullkey");
            return "success";
        }

        [HttpGet]
        [Route("api/CustomAttributesValueNull")]
        public String CustomAttributesValueNull()
        {
            NewRelic.Api.Agent.NewRelic.AddCustomParameter("keywithnullvalue", null);
            return "success";
        }
    }
}
