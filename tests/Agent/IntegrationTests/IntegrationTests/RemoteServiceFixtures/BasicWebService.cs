using System;
using System.IO;
using System.Net;
using System.Xml.Linq;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
	public class BasicWebService : RemoteApplicationFixture
	{
		private const String ApplicationDirectoryName = "BasicWebService";

		public BasicWebService() : base(new RemoteWebApplication(ApplicationDirectoryName, ApplicationType.Bounded))
		{
		}

		public void InvokeServiceHttp()
		{
			var address = $"http://{DestinationServerName}:{Port}/BasicWebService.asmx/HelloWorld";

			var request = (HttpWebRequest)WebRequest.Create(address);
			request.Method = "POST";
			request.ContentType = "application/x-www-form-urlencoded";
			request.ContentLength = 0;
			var response = request.GetResponse();
			var responseStream = response.GetResponseStream();
			var reader = new StreamReader(responseStream);
			var responseString = reader.ReadToEnd();
			reader.Close();
			response.Close();

			Assert.Contains("Hello World", responseString);
		}

		public void InvokeServiceSoap()
		{
			const String soapEnvelope =
			@"<?xml version=""1.0"" encoding=""utf-8""?>
			<soap12:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
			    xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
			    xmlns:soap12=""http://www.w3.org/2003/05/soap-envelope"">
			    <soap12:Body>
			        <HelloWorld xmlns=""BasicWebService"" />
			    </soap12:Body>
			</soap12:Envelope>";

			var doc = XDocument.Parse(soapEnvelope);
			var address = $"http://{DestinationServerName}:{Port}/BasicWebService.asmx/HelloWorld";

			var request = (HttpWebRequest)WebRequest.Create(address);
			request.ContentType = "application/soap+xml; charset=utf-8";
			request.Method = "POST";

			//insert SOAP envelope into the request
			using (var stream = request.GetRequestStream())
			{
				doc.Save(stream);
			}

			var response = request.GetResponse();

			using (var reader = new StreamReader(response.GetResponseStream()))
			{
				Assert.Contains("Hello World", XDocument.Load(reader).ToString());
			}

		}

		public void ThrowExceptionHttp()
		{
			var address = $"http://{DestinationServerName}:{Port}/BasicWebService.asmx/ThrowException";

			var request = (HttpWebRequest)WebRequest.Create(address);
			request.Method = "POST";
			request.ContentType = "application/x-www-form-urlencoded";
			request.ContentLength = 0;

			Assert.Throws<WebException>(() => request.GetResponse());
		}

		public void ThrowExceptionSoap()
		{
			const String soapEnvelope =
			@"<?xml version=""1.0"" encoding=""utf-8""?>
			<soap12:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
			    xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
			    xmlns:soap12=""http://www.w3.org/2003/05/soap-envelope"">
			    <soap12:Body>
			        <ThrowException xmlns=""BasicWebService"" />
			    </soap12:Body>
			</soap12:Envelope>";

			var doc = XDocument.Parse(soapEnvelope);
			var address = $"http://{DestinationServerName}:{Port}/BasicWebService.asmx/ThrowException";

			var request = (HttpWebRequest)WebRequest.Create(address);
			request.ContentType = "application/soap+xml; charset=utf-8";
			request.Method = "POST";

			//insert SOAP envelope into the request
			using (var stream = request.GetRequestStream())
			{
				doc.Save(stream);
			}

			Assert.Throws<WebException>(() => request.GetResponse());

		}
	}
}
