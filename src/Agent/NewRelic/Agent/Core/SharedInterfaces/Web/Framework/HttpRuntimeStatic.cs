// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System;

namespace NewRelic.Agent.Core.SharedInterfaces.Web
{

	public class HttpRuntimeStatic : IHttpRuntimeStatic
	{
		public string AppDomainAppVirtualPath => System.Web.HttpRuntime.AppDomainAppVirtualPath;
	}
}
#endif
