/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
    public class VendorExplainValidationResult
    {
        public bool IsValid { get; }
        public string ValidationMessage { get; }

        public VendorExplainValidationResult(bool isValid) : this(isValid, null)
        {

        }

        public VendorExplainValidationResult(bool isValid, string validationMessage)
        {
            IsValid = isValid;
            ValidationMessage = validationMessage;
        }
    }
}
