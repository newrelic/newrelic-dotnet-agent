﻿using System;
using System.Web;
using JetBrains.Annotations;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.Asp35.Shared
{
	public static class RequestUrlRetriever
	{
		// In System.Web v4.0 (.NET 4 and up), accessing the Request.Url property directly clears a validation flag.  To prevent users from missing 
		// the validation warning, this function instead accesses the backing method.  If it's unavailable, it's safe to assume the System.Web version 
		// is 2.0, and can get Request.Url directly.

		[CanBeNull]
		private static readonly Func<HttpRequest, Func<String>, Uri> GetUnvalidatedRequestUrl;

		static RequestUrlRetriever()
		{
			try
			{
				GetUnvalidatedRequestUrl = VisibilityBypasser.Instance.GenerateOneParameterMethodCaller<HttpRequest, Func<String>, Uri>("BuildUrl");
			}
			catch
			{
				GetUnvalidatedRequestUrl = null;
			}
		}

		[CanBeNull]
		public static Uri TryGetRequestUrl([NotNull] HttpRequest request, Func<String> pathAccessor)
		{
			try
			{
				return GetUnvalidatedRequestUrl?.Invoke(request, pathAccessor) ?? request.Url;
			}
			catch
			{
				return null;
			}
		}
	}
}
