using System;
using System.Collections.Generic;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.Owin
{
    public class OwinTransactionContext
    {
        private const string AssemblyName = "Microsoft.Owin.Host.HttpListener";
        private const string TypeName = "Microsoft.Owin.Host.HttpListener.RequestProcessing.OwinHttpListenerContext";

        private const string TransactionKey = "NewRelic.Owin.Transaction";

        private static Func<object, IDictionary<string, object>> _getContext;
        private static Func<object, IDictionary<string, object>> GetContext => _getContext ?? (_getContext = VisibilityBypasser.Instance.GenerateFieldAccessor<IDictionary<string, object>>(AssemblyName, TypeName, "_environment"));

        public static void SetTransactionOnEnvironment(object callEnvironment, ITransaction transaction)
        {
            var context = callEnvironment as IDictionary<string, object>;
            context?.Add(TransactionKey, transaction);
        }

        public static ITransaction ExtractTransactionFromContext(object owinHttpListenerContext)
        {
            var context = GetContext(owinHttpListenerContext);

            ITransaction transaction = null;
            if ((context != null) && context.ContainsKey(TransactionKey))
            {
                transaction = context[TransactionKey] as ITransaction;
                context.Remove(TransactionKey); //cleanup required to prevent OwinHttpListenerContextEnd from ending transaction again.
            }

            return transaction;
        }
    }
}
