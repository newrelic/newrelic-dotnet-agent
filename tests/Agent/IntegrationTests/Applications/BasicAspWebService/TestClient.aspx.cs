// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;

namespace BasicAspWebService
{
    public partial class TestClient : System.Web.UI.Page
    {

        protected void Page_Load(object sender, EventArgs e)
        {
            NewRelic.Api.Agent.NewRelic.IgnoreTransaction();

            string script = @"
							var helloWorldProxy;
							function pageLoad() {
								helloWorldProxy = new BasicAspWebService.HelloWorld();
								helloWorldProxy.set_defaultSucceededCallback(SucceededCallback);
								helloWorldProxy.set_defaultFailedCallback(FailedCallback);
								var greetings = helloWorldProxy.Greetings();
							}

							function SucceededCallback(result) {
								var RsltElem = document.getElementById(""Results""); 
								RsltElem.innerHTML = result;
							}

							function FailedCallback(error, userContext, methodName){
							if (error !== null)
								{
									var RsltElem = document.getElementById(""Results"");
									RsltElem.innerHTML = ""An error occurred: "" +
									error.get_message();
								}
							}";
            Page.ClientScript.RegisterStartupScript(this.GetType(), "JsFunc", script, true);
        }
    }
}
