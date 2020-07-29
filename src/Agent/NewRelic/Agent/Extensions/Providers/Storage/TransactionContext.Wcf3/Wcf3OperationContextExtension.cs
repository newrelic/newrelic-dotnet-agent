using System.Collections;
using System.ServiceModel;

namespace NewRelic.Providers.Storage.TransactionContext
{
    ///<summary>
    /// This class is an extension of <see cref="InstanceContext"/>.
    ///</summary>
    /// <remarks>
    /// This extension's purpose is to provide a way to store agent context information in
    /// a WCF service's context. The <see cref="InstanceContext"/> exists
    /// within the  <see cref="OperationContext"/>.
    /// 
    /// The <see cref="InstanceContext"/> keeps an internal list of all
    /// extensions. 
    /// </remarks>
    public class Wcf3OperationContextExtension : IExtension<OperationContext>
    {
        private readonly IDictionary _items = new Hashtable();

        ///<summary>
        /// <see cref="IDictionary"/> stored in current instance context.
        ///</summary>
        public IDictionary Items { get { return _items; } }

        ///<summary>
        /// Gets the current instance of <see cref="Wcf3OperationContextExtension"/>
        ///</summary>
        public static Wcf3OperationContextExtension Current
        {
            get
            {
                var currentOperationContext = OperationContext.Current;
                if (currentOperationContext == null)
                    return null;

                var context = currentOperationContext.Extensions.Find<Wcf3OperationContextExtension>();
                if (context != null)
                    return context;

                context = new Wcf3OperationContextExtension();
                currentOperationContext.Extensions.Add(context);
                return context;
            }
        }

        public static bool CanProvide { get { return OperationContext.Current != null; } }

        /// <summary>
        /// <see cref="IExtension{T}"/> Attach() method
        /// </summary>
        public void Attach(OperationContext owner) { }

        /// <summary>
        /// <see cref="IExtension{T}"/> Detach() method
        /// </summary>
        public void Detach(OperationContext owner) { }
    }
}
