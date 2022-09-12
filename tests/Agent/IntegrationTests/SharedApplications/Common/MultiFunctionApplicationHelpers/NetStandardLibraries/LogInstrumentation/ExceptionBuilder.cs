// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.LogInstrumentation
{
    public static class ExceptionBuilder
    {
        public static Exception BuildException(string message)
        {
            try
            {
                throw new Exception(message);
            }
            catch (Exception ex)
            {
                return ex;
            }
        }
    }
}
