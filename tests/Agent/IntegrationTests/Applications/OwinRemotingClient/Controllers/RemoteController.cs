using System;
using System.Web.Http;
using OwinRemotingShared;

namespace OwinRemotingClient.Controllers
{
	[RoutePrefix("Remote")]
	public class RemoteController : ApiController
	{
		[Route("GetObjectTcp")]
		public string GetObjectTcp()
		{
			var myMarshalByRefClassObj = (MyMarshalByRefClass)Activator.GetObject(typeof(MyMarshalByRefClass), "tcp://localhost:9001/GetObject");
			return GetObject(myMarshalByRefClassObj);
		}

		[Route("GetObjectHttp")]
		public string GetObjectHttp()
		{
			var myMarshalByRefClassObj = (MyMarshalByRefClass)Activator.GetObject(typeof(MyMarshalByRefClass), "http://localhost:9002/GetObject");
			return GetObject(myMarshalByRefClassObj);
		}

		private string GetObject(MyMarshalByRefClass myMarshalByRefClassObj)
		{
			var result = "No exception";

			try
			{
				var myReturnValue = myMarshalByRefClassObj.MyMethod();
			}
			catch (Exception ex)
			{
				result = ex.ToString();
			}

			return result;
		}
	}
}

