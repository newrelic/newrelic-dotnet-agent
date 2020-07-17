using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.AspNetCore
{
	public class InvokeActionMethodAsyncWrapper : IWrapper
	{
		private Func<object, ControllerContext> _getControllerContext;
		public Func<object, ControllerContext> GetControllerContext => _getControllerContext ?? (_getControllerContext = VisibilityBypasser.Instance.GenerateFieldAccessor<ControllerContext>("Microsoft.AspNetCore.Mvc.Core", "Microsoft.AspNetCore.Mvc.Internal.ControllerActionInvoker", "_controllerContext"));

		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			return new CanWrapResponse("NewRelic.Providers.Wrapper.AspNetCore.InvokeActionMethodAsync".Equals(methodInfo.RequestedWrapperName));
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
		{
			if (instrumentedMethodCall.IsAsync)
			{
				transaction.AttachToAsync();
			}

			var controllerContext = GetControllerContext(instrumentedMethodCall.MethodCall.InvocationTarget);
			var actionDescriptor = controllerContext.ActionDescriptor;

			var transactionName = CreateTransactionName(actionDescriptor);

			transaction.SetWebTransactionName(WebTransactionType.MVC, transactionName, 6);

			//Framework uses ControllerType.Action for these metrics & transactions. WebApi is Controller.Action for both
			//Taking opinioned stance to do ControllerType.MethodName for segments. Controller/Action for transactions
			var controllerTypeName = controllerContext.ActionDescriptor.ControllerTypeInfo.Name;
			var methodName = controllerContext.ActionDescriptor.MethodInfo.Name;

			var segment = transaction.StartMethodSegment(instrumentedMethodCall.MethodCall, controllerTypeName, methodName);

			return Delegates.GetDelegateFor<Task>(
				onFailure: segment.End,
				onSuccess: HandleSuccess
			);

			void HandleSuccess(Task task)
			{
				segment.RemoveSegmentFromCallStack();

				if (task == null)
				{
					return;
				}
				
				task.ContinueWith(OnTaskCompletion);
			}

			void OnTaskCompletion(Task completedTask)
			{
				try
				{
					segment.End();
				}
				catch (Exception ex)
				{
					agentWrapperApi.SafeHandleException(ex);
				}
			}
		}

		private static string CreateTransactionName(ControllerActionDescriptor actionDescriptor)
		{
			var controllerName = actionDescriptor.ControllerName;
			var actionName = actionDescriptor.ActionName;

			var transactionName = $"{controllerName}/{actionName}";

			foreach (var parameter in actionDescriptor.Parameters)
			{
				transactionName += "/{" + parameter.Name + "}";
			}

			return transactionName;
		}
	}
}
