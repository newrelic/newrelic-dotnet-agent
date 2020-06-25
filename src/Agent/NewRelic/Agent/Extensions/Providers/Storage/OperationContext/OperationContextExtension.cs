/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System.Collections;
using System.ServiceModel;

namespace NewRelic.Providers.Storage.OperationContext
{
    ///<summary>
    /// This class is an extension of <see cref="System.ServiceModel.InstanceContext"/>.
    ///</summary>
    /// <remarks>
    /// This extension's purpose is to provide a way to store agent context information in
    /// a WCF service's context. The <see cref="System.ServiceModel.InstanceContext"/> exists
    /// within the  <see cref="System.ServiceModel.OperationContext"/>.
    /// 
    /// The <see cref="System.ServiceModel.InstanceContext"/> keeps an internal list of all
    /// extensions. 
    /// </remarks>
    public class OperationContextExtension : IExtension<System.ServiceModel.OperationContext>
    {
        private readonly IDictionary _items = new Hashtable();

        ///<summary>
        /// <see cref="IDictionary"/> stored in current instance context.
        ///</summary>
        public IDictionary Items { get { return _items; } }

        ///<summary>
        /// Gets the current instance of <see cref="OperationContextExtension"/>
        ///</summary>
        public static OperationContextExtension Current
        {
            get
            {
                var currentOperationContext = System.ServiceModel.OperationContext.Current;
                if (currentOperationContext == null)
                    return null;

                var context = currentOperationContext.Extensions.Find<OperationContextExtension>();
                if (context != null)
                    return context;

                context = new OperationContextExtension();
                currentOperationContext.Extensions.Add(context);
                return context;
            }
        }

        public static bool CanProvide { get { return System.ServiceModel.OperationContext.Current != null; } }

        /// <summary>
        /// <see cref="IExtension{T}"/> Attach() method
        /// </summary>
        public void Attach(System.ServiceModel.OperationContext owner) { }

        /// <summary>
        /// <see cref="IExtension{T}"/> Detach() method
        /// </summary>
        public void Detach(System.ServiceModel.OperationContext owner) { }
    }
}
