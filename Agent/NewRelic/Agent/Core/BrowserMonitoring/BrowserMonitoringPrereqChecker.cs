using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Core.Logging;

namespace NewRelic.Agent.Core.BrowserMonitoring
{
	public interface IBrowserMonitoringPrereqChecker
	{
		/// <summary>
		/// Returns true if RUM should be injected. Use if requestPath and contentType are not known.
		/// </summary>
		Boolean ShouldManuallyInject([NotNull] IInternalTransaction transaction);

		/// <summary>
		/// Returns true if RUM should be injected. Use if requestPath and contentType are known.
		/// </summary>
		Boolean ShouldAutomaticallyInject([NotNull] IInternalTransaction transaction, [CanBeNull] String requestPath, [NotNull] String contentType);
	}

	public class BrowserMonitoringPrereqChecker : IBrowserMonitoringPrereqChecker
	{
		[NotNull]
		private readonly IConfigurationService _configurationService;

		public BrowserMonitoringPrereqChecker([NotNull] IConfigurationService configurationService)
		{
			_configurationService = configurationService;
		}

		public Boolean ShouldManuallyInject(IInternalTransaction transaction)
		{
			if (!IsValidBrowserMonitoringJavaScriptAgentLoaderType())
				return false;

			return !transaction.IgnoreAllBrowserMonitoring;
		}
		
		public Boolean ShouldAutomaticallyInject(IInternalTransaction transaction, String requestPath, String contentType)
		{
			if (!IsValidBrowserMonitoringJavaScriptAgentLoaderType())
				return false;

			if (!_configurationService.Configuration.BrowserMonitoringAutoInstrument)
				return false;

			if (transaction.IgnoreAutoBrowserMonitoring || transaction.IgnoreAllBrowserMonitoring)
				return false;
			
			if (!IsHtmlContent(contentType))
				return false;

			// Perform this check last due to its potentially large cost
			if (!BrowserInstrumentationAllowedForUrlPath(requestPath, _configurationService.Configuration.RequestPathExclusionList))
				return false;

			return true;
		}

		private Boolean IsValidBrowserMonitoringJavaScriptAgentLoaderType()
		{
			var loaderType = _configurationService.Configuration.BrowserMonitoringJavaScriptAgentLoaderType;
			var isValid = !loaderType.Equals("none", StringComparison.InvariantCultureIgnoreCase);

			return isValid;
		}

		private static Boolean IsHtmlContent([NotNull] String contentType)
		{
			if (String.IsNullOrEmpty(contentType))
				return false;

			try
			{
				return new ContentType(contentType).MediaType == "text/html";
			}
			catch (Exception ex)
			{
				Log.Debug($"Unable to parse content type: {ex}");
				return false;
			}
		}

		/// <summary>
		/// Determines whether the agent's Javascript Instrumentation should be injected for
		/// a page request whose <see cref="System.Web.HttpRequest.Path" /> is <paramref name="requestPath"/>.
		/// </summary>
		/// <remarks>
		/// Since we're building against .NET 2.0/3.5, we cannot take advantage of a .NET 4.5 addition 
		/// to System.Text.RegularExpressions.Regex which allows you to pass a TimeSpan as a timeout.
		/// This requires a little explanation: Regular expression evaluation can take a long time 
		/// (multiple seconds even!) depending on the complexity of the expression. Prior to .NET 4.5,
		/// a Regex either would never time out, or, if an application domain-specific timeout existed
		/// in the current application domain, it would timeout based on that timeout setting. Upon
		/// timing out, the Regex evaluation statement (e.g., IsMatch) would throw a 
		/// <see cref="System.Text.RegularExpressions.RegexMatchTimeoutException" />.
		/// 
		/// In our case, we cannot set a regular expression-specific timeout since we're not building 
		/// against .NET 4.5. Since we are running in another application's process, it is not advisable
		/// for us to set the application domain's regular expression timeout. So, we have to simply catch
		/// that timeout-based exception to be safe. For simplicity, this method catches all exceptions although
		/// it is unlikely anything but a timeout exception would occur.
		/// 
		/// For further reference, see http://msdn.microsoft.com/en-us/library/system.text.regularexpressions.regex(v=vs.110).aspx
		/// and http://softwareninjaneer.com/posts/regex-engine-updated-to-allow-timeouts-in-net-4-5/
		/// </remarks>
		/// <param name="requestPath">The requestPath to be evaluated for blacklisting.</param>
		/// <param name="requestPathExclusionList">A list of Regex's. </param>
		/// <returns>True if browser instrumentation should occur for the page request.</returns>
		private static Boolean BrowserInstrumentationAllowedForUrlPath([CanBeNull] String requestPath, [NotNull] IEnumerable<Regex> requestPathExclusionList)
		{
			if (String.IsNullOrEmpty(requestPath))
				return true;

			return requestPathExclusionList
				.Where(regex => regex != null)
				.All(regex => !IsMatch(requestPath, regex));
		}

		private static Boolean IsMatch([NotNull] String path, [NotNull] Regex regex)
		{
			try
			{
				if (regex.IsMatch(path))
					return true;

				return false;
			}
			catch (Exception e)
			{
				Log.Error($"Exception attempting to validate request path for Browser Instrumentation blacklisting: {e}");
				return false;
			}
		}
	}
}
