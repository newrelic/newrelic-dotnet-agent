using System;
using System.Web.Services;

namespace BasicWebService
{
    [WebService(Namespace = "BasicWebService")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    public class TestWebService : WebService
    {
        [WebMethod]
        public string HelloWorld()
        {
            return "Hello World";
        }

        [WebMethod]
        public string ThrowException()
        {
            throw new Exception("Oh no!");
        }
    }
}
