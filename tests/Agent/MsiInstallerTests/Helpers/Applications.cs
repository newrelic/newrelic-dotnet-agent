/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
namespace FunctionalTests.Helpers
{
    public class Applications
    {
        public static Application DotNet_Functional_InstallTestApp = new Application { Name = "DotNet-Functional-InstallTestApp", BaseUrlFormatter = "http://{0}/DotNet-Functional-InstallTestApp/" };
    }
}
