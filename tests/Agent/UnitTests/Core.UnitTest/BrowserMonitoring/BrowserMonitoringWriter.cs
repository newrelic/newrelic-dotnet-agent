// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Xml.Linq;
using System;
using NUnit.Framework;

namespace NewRelic.Agent.Core.BrowserMonitoring
{
    [TestFixture, Category("BrowserMonitoring")]
    public class Class_BrowserMonitoringWriter
    {
        private readonly string _jsScript =
                    "<script type=\"text/javascript\">window.NREUM||(NREUM={});NREUM.info = {\"beacon\":\"staging-beacon-N.newrelic.com\",\"errorBeacon:\":\"staging-jserror.newrelic.com\",\"licenseKey\":\"a4fa192fe5\",\"applicationID\":\"48102\",\"transactionName\":\"ZlIAbEACVxFYVkBbDV8YJldGLVwWelpaRhBeWw5dQExxDVRQG3sMVVIa\",\"queueTime\":\"0\",\"applicationTime\":\"37\", \"customString\":\"$1\", \"ttGuid\":\"BD0436479135FF3F\",\"agent\":\"js-agent.newrelic.com/nr-248.min.js\"}</script>";

        [Test]
        public void empty_input_results_in_empty_output()
        {
            // ARRANGE
            string content = string.Empty;

            // ACT
            BrowserMonitoringWriter writer = new BrowserMonitoringWriter(() => _jsScript);
            string modifiedContent = writer.WriteScriptHeaders(content);

            // ASSERT
            Assert.That(modifiedContent, Is.EqualTo(string.Empty));
        }

        [Test]
        public void html_without_head_tag_or_spaces()
        {
            // ARRANGE
            string content = "<html><body><p>YoAdrian!</p></body></html>";
            string expected = string.Format("<html>{0}<body><p>YoAdrian!</p></body></html>", _jsScript);

            // ACT
            BrowserMonitoringWriter writer = new BrowserMonitoringWriter(() => _jsScript);
            string modifiedContent = writer.WriteScriptHeaders(content);

            // ASSERT
            Assert.That(modifiedContent, Is.EqualTo(expected));
        }

        [Test]
        public void html_without_head_tag_but_with_spaces()
        {
            // ARRANGE
            string content = "<html> <body><p>Yo Adrian!</p> </body></html>";
            string expected = string.Format("<html> {0}<body><p>Yo Adrian!</p> </body></html>", _jsScript);

            // ACT
            BrowserMonitoringWriter writer = new BrowserMonitoringWriter(() => _jsScript);
            string modifiedContent = writer.WriteScriptHeaders(content);

            // ASSERT
            Assert.That(modifiedContent, Is.EqualTo(expected));
        }

        [Test]
        public void html_without_head_tag_but_with_body_text_in_script()
        {
            // ARRANGE
            string content = "<html> <body><script type=\"text/javascript\">var thing = \"<body\";</script><p>Yo Adrian!</p> </body></html>";
            string expected = string.Format("<html> {0}<body><script type=\"text/javascript\">var thing = \"<body\";</script><p>Yo Adrian!</p> </body></html>", _jsScript);

            // ACT
            BrowserMonitoringWriter writer = new BrowserMonitoringWriter(() => _jsScript);
            string modifiedContent = writer.WriteScriptHeaders(content);

            // ASSERT
            Assert.That(modifiedContent, Is.EqualTo(expected));
        }

        [Test]
        public void html_with_self_closing_head_tag()
        {
            // ARRANGE
            string content = "<html><head/><body><p>YoAdrian!</p></body></html>";
            string expected = string.Format("<html><head/>{0}<body><p>YoAdrian!</p></body></html>", _jsScript);

            // ACT
            BrowserMonitoringWriter writer = new BrowserMonitoringWriter(() => _jsScript);
            string modifiedContent = writer.WriteScriptHeaders(content);

            // ASSERT
            Assert.That(modifiedContent, Is.EqualTo(expected));
        }

        [Test]
        public void html_with_simple_opening_and_closing_head_tags()
        {
            // ARRANGE
            string contentUpToHead = "<html><head>";
            string contentRemaining = "</head><body><p>YoAdrian!</p></body></html>";
            string expected = string.Format("{0}{1}{2}", contentUpToHead, _jsScript, contentRemaining);

            // ACT
            BrowserMonitoringWriter writer = new BrowserMonitoringWriter(() => _jsScript);
            string modifiedContent = writer.WriteScriptHeaders(contentUpToHead + contentRemaining);

            // ASSERT
            Assert.That(modifiedContent, Is.EqualTo(expected));
        }

        [Test]
        public void html_with_head_tag_with_attributes()
        {
            // ARRANGE
            string content = "<html><head bestevar='Boston Red Sox'></head><body><p>YoAdrian!</p></body></html>";
            string expected = string.Format("<html><head bestevar='Boston Red Sox'>{0}</head><body><p>YoAdrian!</p></body></html>", _jsScript);

            // ACT
            BrowserMonitoringWriter writer = new BrowserMonitoringWriter(() => _jsScript);
            string modifiedContent = writer.WriteScriptHeaders(content);

            // ASSERT
            Assert.That(modifiedContent, Is.EqualTo(expected));
        }

        [Test]
        public void html_with_head_tag_and_spaces()
        {
            // ARRANGE
            string content = "<html><head   ></head><body><p>YoAdrian!</p></body></html>";
            string expected = string.Format("<html><head   >{0}</head><body><p>YoAdrian!</p></body></html>", _jsScript);

            // ACT
            BrowserMonitoringWriter writer = new BrowserMonitoringWriter(() => _jsScript);
            string modifiedContent = writer.WriteScriptHeaders(content);

            // ASSERT
            Assert.That(modifiedContent, Is.EqualTo(expected));
        }

        [Test]
        public void html_with_head_tag_and_X_UA_COMPATIBLE_meta_tag()
        {
            // ARRANGE
            string contentUpToXUATag = "<html><head>" +
                                       "<meta http-equiv=\"X-UA-Compatible\" content=\"IE=9\">";
            string contentRemaining = "</head><body><p>YoAdrian!</p></body></html>";

            string expected = string.Format("{0}{1}{2}", contentUpToXUATag, _jsScript, contentRemaining);

            // ACT
            BrowserMonitoringWriter writer = new BrowserMonitoringWriter(() => _jsScript);
            string modifiedContent = writer.WriteScriptHeaders(contentUpToXUATag + contentRemaining);

            // ASSERT
            Assert.That(modifiedContent, Is.EqualTo(expected));
        }

        [Test]
        public void html_with_head_tag_and_X_UA_COMPATIBLE_meta_tag_with_lotta_spaces()
        {
            // ARRANGE
            string contentUpToXUATag = "<html> <head  >" +
                                       "<meta http-equiv     =              \"X-UA-Compatible\"      content=\"IE=9\">";
            string contentRemaining = "</head>        <body>  <p>    YoAdrian!</p>   </body></html>";

            string expected = string.Format("{0}{1}{2}", contentUpToXUATag, _jsScript, contentRemaining);

            // ACT
            BrowserMonitoringWriter writer = new BrowserMonitoringWriter(() => _jsScript);
            string modifiedContent = writer.WriteScriptHeaders(contentUpToXUATag + contentRemaining);

            // ASSERT
            Assert.That(modifiedContent, Is.EqualTo(expected));
        }

        [Test]
        public void html_with_head_tag_X_UA_COMPATIBLE_meta_tag_and_other_head_tags()
        {
            // ARRANGE
            string contentUpToXUATag = "<html><head>" +
                                       "<meta charset=\"utf-8\" />" +
                                       "<title>Home Page - My ASP.NET MVC Application</title>" +
                                       "<link href=\"/MVC4-WebApp-Performance/favicon.ico\" rel=\"shortcut icon\" type=\"image/x-icon\" />" +
                                       "<meta name=\"viewport\" content=\"width=device-width\" />" +
                                       "<meta http-equiv=\"X-UA-Compatible\" content=\"IE=9\">";
            string contentRemaining = "<link href=\"/MVC4-WebApp-Performance/Content/site.css\" rel=\"stylesheet\"/>" +
                "<script src=\"/MVC4-WebApp-Performance/Scripts/modernizr-2.6.2.js\"></script>" +
                "<script src=\"/mvc3_rum/Scripts/jquery-1.7.1.min.js\" type=\"text/javascript\"></script>" +
                "<script src=\"/mvc3_rum/Scripts/modernizr-2.5.3.js\" type=\"text/javascript\"></script>" +
                "</head><body><p>YoAdrian!</p></body></html>";

            string expected = string.Format("{0}{1}{2}", contentUpToXUATag, _jsScript, contentRemaining);

            // ACT
            BrowserMonitoringWriter writer = new BrowserMonitoringWriter(() => _jsScript);
            string modifiedContent = writer.WriteScriptHeaders(contentUpToXUATag + contentRemaining);

            // ASSERT
            Assert.That(modifiedContent, Is.EqualTo(expected));
        }

        [Test]
        public void html_with_simple_openclose_head_tags_and_script_with_embedded_head()
        {
            // ARRANGE
            string contentUpToHead = "<html><head>";
            string contentPartTwo = "</head><body><p>YoAdrian!</p>";
            string contentPartThree = "<script type=\"text/javascript\">function another_head() {" +
                                      "document.write(\"<script type=\"text/javascript\"> <head></head> }" +
                                      "</script></body></html>";

            string expected = string.Format("{0}{1}{2}{3}", contentUpToHead, _jsScript, contentPartTwo, contentPartThree);

            // ACT
            BrowserMonitoringWriter writer = new BrowserMonitoringWriter(() => _jsScript);
            string modifiedContent = writer.WriteScriptHeaders(contentUpToHead + contentPartTwo + contentPartThree);

            // ASSERT
            Assert.That(modifiedContent, Is.EqualTo(expected));
        }

        [Test]
        public void html_with_simple_openclose_head_tags_and_script_with_embedded_head_that_has_X_UA_COMPATIBLE_meta_tag()
        {
            // ARRANGE
            string contentUpToHead = "<html><head>";
            string contentPartTwo = "</head><body><p>YoAdrian!</p>";
            string contentPartThree = "<script type=\"text/javascript\">function another_head() {" +
                                      "document.write(\"<script type=\"text/javascript\"> <head><meta http-equiv=\"X-UA-Compatible\" content=\"IE=9\"></head> }" +
                                      "</script></body></html>";

            string expected = string.Format("{0}{1}{2}{3}", contentUpToHead, _jsScript, contentPartTwo, contentPartThree);

            // ACT
            BrowserMonitoringWriter writer = new BrowserMonitoringWriter(() => _jsScript);
            string modifiedContent = writer.WriteScriptHeaders(contentUpToHead + contentPartTwo + contentPartThree);

            // ASSERT
            Assert.That(modifiedContent, Is.EqualTo(expected));
        }

        [Test]
        public void cross_agent_browser_monitor_injection_from_text()
        {
            var data = "<html><head /><body>im some body text</body></html>";
            var expected = "<html><head />EXPECTED_RUM_LOADER_LOCATION<body>im some body text</body></html>";
            var writer = new BrowserMonitoringWriter(() => "EXPECTED_RUM_LOADER_LOCATION");
            var result = writer.WriteScriptHeaders(data);
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void cross_agent_browser_monitor_injection_from_insane_text()
        {
            var angelicText = @"
                // c# special - verbatim
                @if(1)@""c:\\documents\x0041\\\files\\\\u0066.txt""@\""{{This}} is the last \u0063hance\x0021\""
                // c# special - interpolation
                $""Hello, {DateTime.Now:F3,-7}!""$"" \""\{X}, {Y}\\""\\ is {Math.Sqrt(X * X + Y * Y)}""$$""""""{{{X}}, {{Y}}} is {{Math.Sqrt(X * X + Y * Y)}}""""""
                // combo
                @$""@${var}$@{{var}}\{var\}\{\""var""{\}\}\\{\\{{var\}}\\}${}$${}${{}}""$@""@${var}$@{{var}}\{var\}\{\""var""{\}\}\\{\\{{var\}}\\}${}$${}${{}}""
                // some bad chars
                ÁáĆćǴ ǵíĹĺŃńŔŕŚ śÝý€ƒ   ©®™œ£¶•

                ¿¡\0\x0\u0000
                // .net regex && substitutions
                $1$2$9$13${name}${c}${re}$$$&$`$'$+$_\p{Sc}*(\s?\d+[.,]?\d*)\p{Sc}*\$&\
                // .net escape chars
                .$^{[(|)*+?\\a\b\t\r\v\f\n\e\040\x9f\cC\cD\*\G(.+)[\t\u007c](.+)\r?\n\$\\$\\\$\$$\$$$\@\\@\\\@\@@\@@@
                ";


            var data = "<html><head /><body>im some body text</body></html>";
            var expected = $"<html><head />{angelicText}<body>im some body text</body></html>";
            var writer = new BrowserMonitoringWriter(() => angelicText);
            var result = writer.WriteScriptHeaders(data);
            Assert.That(result, Is.EqualTo(expected));
        }
    }
}
