// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


// From https://github.com/dotnet/corefx/blob/master/src/System.Diagnostics.DiagnosticSource/src/System/Diagnostics/DiagnosticSourceEventSource.cs

using System;
using System.Reflection;

namespace NewRelic.OpenTracing.AmazonLambda.DiagnosticObserver
{
    internal class PropertyFetcher
    {
        private readonly string _propertyName;
        private Type _expectedType;
        private PropertyFetch _fetchForExpectedType;

        /// <summary>
        /// Make a new PropertyFetcher for a property named 'propertyName'.
        /// </summary>
        public PropertyFetcher(string propertyName)
        {
            _propertyName = propertyName;
        }

        /// <summary>
        /// Given an object fetch the property that this PropertySpec represents.
        /// </summary>
        public object Fetch(object obj)
        {
            var objType = obj.GetType();
            if (objType != _expectedType)
            {
                var typeInfo = objType.GetTypeInfo();
                _fetchForExpectedType = PropertyFetch.FetcherForProperty(typeInfo.GetDeclaredProperty(_propertyName));
                _expectedType = objType;
            }
            return _fetchForExpectedType.Fetch(obj);
        }

        /// <summary>
        /// PropertyFetch is a helper class. It takes a PropertyInfo and then knows how
        /// to efficiently fetch that property from a .NET object (See Fetch method).
        /// It hides some slightly complex generic code.  
        /// </summary>
        private class PropertyFetch
        {
            /// <summary>
            /// Create a property fetcher from a .NET Reflection PropertyInfo class that
            /// represents a property of a particular type.
            /// </summary>
            public static PropertyFetch FetcherForProperty(PropertyInfo propertyInfo)
            {
                if (propertyInfo == null)
                    return new PropertyFetch();     // returns null on any fetch.

                var typedPropertyFetcher = typeof(TypedFetchProperty<,>);
                var instantiatedTypedPropertyFetcher = typedPropertyFetcher.GetTypeInfo().MakeGenericType(
                    propertyInfo.DeclaringType, propertyInfo.PropertyType);

                return (PropertyFetch)Activator.CreateInstance(instantiatedTypedPropertyFetcher, propertyInfo);
            }

            /// <summary>
            /// Given an object, fetch the property that this propertyFech represents.
            /// </summary>
            public virtual object Fetch(object obj)
            {
                return null;
            }

            private class TypedFetchProperty<TObject, TProperty> : PropertyFetch
            {
                public TypedFetchProperty(PropertyInfo property)
                {
                    _propertyFetch = (Func<TObject, TProperty>)property.GetMethod.CreateDelegate(typeof(Func<TObject, TProperty>));
                }
                public override object Fetch(object obj)
                {
                    return _propertyFetch((TObject)obj);
                }
                private readonly Func<TObject, TProperty> _propertyFetch;
            }
        }
    }
}

