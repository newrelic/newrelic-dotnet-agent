using System;
using System.Linq;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.HttpWebRequest
{
	public class SerializeHeadersWrapper : IWrapper
	{
		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;
			var canWrap = method.MatchesAny(assemblyName: "System", typeName: "System.Net.HttpWebRequest", methodName: "SerializeHeaders");
			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
		{
			var request = (System.Net.HttpWebRequest)instrumentedMethodCall.MethodCall.InvocationTarget;

			if (request == null)
			{ 
				throw new NullReferenceException(nameof(request));
			}

			if (request.Headers == null)
			{
				throw new NullReferenceException("request.Headers");
			}


			var setHeaders = new Action<string, string>((key, value) =>
			{
				request.Headers?.Set(key, value);
			});

			if (!agent.Configuration.W3CEnabled)
			{
				var headers = transaction.GetRequestMetadata()
				.Where(header => header.Key != null);

				foreach (var header in headers)
				{
					setHeaders(header.Key, header.Value);
				}
			}
			else
			{
				try
				{
					transaction.InsertDistributedTraceHeaders(setHeaders);
				}
				catch (Exception ex)
				{
					agent.HandleWrapperException(ex);
				}
			}

			return Delegates.NoOp;
		}
	}
}
