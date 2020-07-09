#if NET35
using System;

namespace NewRelic.SystemInterfaces.Web
{

	public class HttpRuntimeStatic : IHttpRuntimeStatic
	{
		public string AppDomainAppVirtualPath => System.Web.HttpRuntime.AppDomainAppVirtualPath;
	}
}
#endif
