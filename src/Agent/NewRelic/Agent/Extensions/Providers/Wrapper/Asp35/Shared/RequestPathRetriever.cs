using System;
using System.Web;
using JetBrains.Annotations;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.Asp35.Shared
{
	public static class RequestPathRetriever
	{
		// In System.Web v4.0 (.NET 4 and up), accessing the Request.Path property directly clears a validation flag.  To prevent users from missing 
		// the validation warning, this function instead accesses the backing method.  If it's unavailable, it's safe to assume the System.Web version 
		// is 2.0, and can get Request.Path directly.

		[CanBeNull]
		private static readonly Func<HttpRequest, String> GetUnvalidatedRequestPath;

		static RequestPathRetriever()
		{
			try
			{
				GetUnvalidatedRequestPath = VisibilityBypasser.Instance.GenerateParameterlessMethodCaller<HttpRequest, String>("GetUnvalidatedPath");
			}
			catch
			{
				GetUnvalidatedRequestPath = null;
			}
		}

		[CanBeNull]
		public static String TryGetRequestPath([NotNull] HttpRequest request)
		{
			try
			{
				return GetUnvalidatedRequestPath?.Invoke(request) ?? request.Path;
			}
			catch
			{
				return null;
			}
		}
	}
}
