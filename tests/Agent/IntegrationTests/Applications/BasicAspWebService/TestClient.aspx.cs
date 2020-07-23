using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace BasicAspWebService
{
    public partial class TestClient : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
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
