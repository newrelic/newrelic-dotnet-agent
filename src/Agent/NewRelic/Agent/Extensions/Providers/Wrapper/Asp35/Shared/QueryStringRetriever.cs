﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Web;
using JetBrains.Annotations;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;
using NewRelic.SystemExtensions.Collections;

namespace NewRelic.Providers.Wrapper.Asp35.Shared
{
	/// <summary>
	/// Surprisingly, the logic necessary to *safely* gather query strings in ASP is actually quite complicated.
	/// 
	/// We cannot simply use HttpContext.Request.QueryString because referencing that property has side-effects. The first time that property is referenced it will also result in the query string being validated. If the query string is invalid then an exception will be thrown. If we swallow that exception then we effectively disable query string validation in the user's application.
	/// 
	/// Unfortunately we cannot simply "re-enable" the validation flag because the flag is stored using an internal type, SimpleBitVector32. Even if we could access that type it would be very brittle. We also cannot just instrument the validation function because the validation function only runs if someone happens to access the query string.
	/// 
	/// There is a backing field, _queryString, but it is *sometimes* populated lazily and the logic for populating that field is quite complicated and also uses internal types. Fortunately, there is a private method, EnsureQueryString, which will populate that backing field if it isn't already populated. Unfortunately, that method only exists in .NET 4.0 and above, which means we cannot use it in .NET 3.5.
	/// 
	/// So we are stuck with a handful of partial solutions, none of which guarantee that we'll be able to retrieve the query string consistently and safely. This class tries to tie a couple different solutions together such that we have a good chance of retrieving the query string without any danger of breaking validation. This class will always retrieve the query string successfully in .NET 4.0+, and will retrieve the query string successfully in .NET 3.5- only if the QueryString property was already accessed at least once before.
	/// </summary>
	public static class QueryStringRetriever
	{
		[CanBeNull]
		private static readonly Func<HttpRequest, NameValueCollection> EnsureQueryString;

		[CanBeNull]
		private static readonly Func<HttpRequest, NameValueCollection> GetQueryStringBackingField;

		static QueryStringRetriever()
		{
			try
			{
				EnsureQueryString = VisibilityBypasser.Instance.GenerateParameterlessMethodCaller<HttpRequest, NameValueCollection>("EnsureQueryString");
			}
			catch
			{
				// In ASP.NET 3.5 the EnsureQueryString method does not exist -- nothing we can do about that.
			}

			try
			{
				GetQueryStringBackingField = VisibilityBypasser.Instance.GenerateFieldAccessor<HttpRequest, NameValueCollection>("_queryString");
			}
			catch
			{
				// Every known version of ASP.NET has a _queryString field, but we wrap this in a try/catch anyway so help future-proof ourselves.
			}
		}

		[CanBeNull]
		private static NameValueCollection TryGetQueryString([NotNull] HttpRequest request, [NotNull] IAgentWrapperApi agentWrapperApi)
		{
			if (EnsureQueryString != null)
				return EnsureQueryString(request);


			if (GetQueryStringBackingField != null)
				return GetQueryStringBackingField(request);

			// If we don't have any way of grabbing the query string then our instrumentation is incomplete, likely because a new version of ASP.NET was released with unexpected changes.
			agentWrapperApi.HandleWrapperException(new NullReferenceException(nameof(GetQueryStringBackingField)));
			return null;
		}

		[CanBeNull]
		public static IDictionary<String, String> TryGetQueryStringAsDictionary([NotNull] HttpRequest request, [NotNull] IAgentWrapperApi agentWrapperApi)
		{
			try
			{
				// Enumerating the NameValueCollection can throw an exception if there are invalid values in the collection. Only applies to .NET 4.5+ apps using requestValidationMode 4.5+.
				// http://referencesource.microsoft.com/#System.Web/HttpRequest.cs,2646
				return TryGetQueryString(request, agentWrapperApi)?.ToDictionary();
			}
			catch
			{
				return null;
			}
		}
	}
}
