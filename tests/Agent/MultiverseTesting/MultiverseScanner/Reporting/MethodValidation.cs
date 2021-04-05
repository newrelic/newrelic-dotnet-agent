// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.MultiverseScanner.ExtensionSerialization;

namespace NewRelic.Agent.MultiverseScanner.Reporting
{
    public class MethodValidation
    {
        public string MethodSignature { get; }

        public bool IsValid { get; set; }

        public MethodValidation(ExactMethodMatcher exactMethodMatcher, bool isValid)
        {
            MethodSignature = exactMethodMatcher.MethodSignature;
            IsValid = isValid;
        }
    }
}
