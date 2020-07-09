using System;

namespace NewRelic.SystemInterfaces.Web
{
	public interface IHttpRuntimeStatic
	{
		String AppDomainAppVirtualPath { get; }
	}
}