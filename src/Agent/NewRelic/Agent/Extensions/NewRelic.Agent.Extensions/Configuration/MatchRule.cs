// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


namespace NewRelic.Agent.Configuration
{
    public abstract class MatchRule
    {
        public abstract bool IsMatch(string input);
    }

    public class StatusCodeExactMatchRule : MatchRule
    {
        private int _statusCode;

        public static StatusCodeExactMatchRule GenerateRule(string statusCode)
        {
            if (int.TryParse(statusCode, out int result))
            {
                return new StatusCodeExactMatchRule(result);
            }

            return null;
        }

        private StatusCodeExactMatchRule(int statusCode)
        {
            _statusCode = statusCode;
        }

        public override bool IsMatch(string input)
        {
            if(int.TryParse(input, out int result))
            {
                return _statusCode == result;
            }

            return false;
        }
    }

    public class StatusCodeInRangeMatchRule : MatchRule
    {
        private int _lower;
        private int _upper;

        public static StatusCodeInRangeMatchRule GenerateRule(string lowerStatusCode, string upperStatusCode)
        {
            if (int.TryParse(lowerStatusCode, out int lower) && int.TryParse(upperStatusCode, out int upper))
            {
                return new StatusCodeInRangeMatchRule(lower, upper);
            }

            return null;
        }

        private StatusCodeInRangeMatchRule(int lower, int upper)
        {
            _lower = lower;
            _upper = upper;
        }

        public override bool IsMatch(string statusCode)
        {
            if (int.TryParse(statusCode, out int result))
            {
                if (result >= _lower && result <= _upper)
                {
                    return true;
                }

                return false;
            }

            return false;
        }
    }
}
