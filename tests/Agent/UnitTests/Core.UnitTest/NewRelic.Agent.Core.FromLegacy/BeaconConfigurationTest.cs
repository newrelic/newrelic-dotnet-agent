using System.Collections.Generic;
using System.IO;
using System.Web;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core
{


    [TestFixture]
    class BeaconConfigurationTest
    {
        public const string JS_AGENT_LOADER = "TESTAgentLoader";
        public const string BROWSER_MONITORING_LOADER_VERSION = "JsAgentLoader.version";
        public const string ERROR_BEACON = "error-beacon.newrelic.com";
        public const string JS_AGENT_FILE = "js-agent-file";

        IDictionary<string, object> ConnectionResponse;

        private const string NRAGENT_COOKIE_VALUE = "tk=0123456789ABCDEF";
        private const string NRAGENT_COOKIE_NAME = "NRAGENT";
        protected IAgent agent;

        [SetUp]
        public void SetUp()
        {
            ConnectionResponse = new Dictionary<string, object>() {
                    { "agent_run_id", 666 },
                    { "data_report_period", 60 },
                    { "application_id", "123"},
                    { "beacon", "http://dude.com"},
                    { "browser_key", "testkey"},
                    { "js_agent_loader", JS_AGENT_LOADER},
                    { "browser_monitoring.loader_version", BROWSER_MONITORING_LOADER_VERSION},
                    { "error_beacon", ERROR_BEACON},
                    { "js_agent_file", JS_AGENT_FILE}

                };

            this.agent = Mock.Create<IAgent>();
        }

        [Test]
        public void Sandbox()
        {
            HttpRequest request = new HttpRequest("~/dude.aspx", "http://localhost/dude", "test=a1&password=dude");
            request.Cookies.Add(new HttpCookie(NRAGENT_COOKIE_NAME, NRAGENT_COOKIE_VALUE));
            using (var stream = new MemoryStream())
            {
                using (TextWriter writer = new StreamWriter(stream))
                {
                    HttpResponse response = new HttpResponse(writer);
                    HttpContext context = new HttpContext(request, response);
                    Assert.That(request.Cookies[NRAGENT_COOKIE_NAME].Value.Equals(NRAGENT_COOKIE_VALUE));

                }
            }
        }
    }
}
