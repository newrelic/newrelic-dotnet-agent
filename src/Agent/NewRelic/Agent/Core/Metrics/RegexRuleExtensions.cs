using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Metric;

namespace NewRelic.Agent.Core.Metrics
{
    public static class RegexRuleExtensions
    {
        [Pure]
        public static RuleResult ApplyTo(this RegexRule regexRule, [CanBeNull] String url)
        {
            // Break the URL in chunks based on the rule configuration
            var chunks = GetChunks(url, regexRule);

            // Apply the rule to each chunk
            Boolean anyChunkUpdated;
            var updatedChunks = ApplyRuleToChunks(regexRule, chunks, out anyChunkUpdated);

            // If the rule didn't apply to any chunk then return a NoMatch result
            if (!anyChunkUpdated)
                return RuleResult.NoMatch;

            // If the rule applied to at least one chunk and the ignore flag is true then return an Ignore result
            if (regexRule.Ignore)
                return RuleResult.Ignore;

            // Otherwise return a successful result with the updated URL
            var path = String.Join(MetricNames.PathSeparator, updatedChunks.ToArray());
            return new RuleResult(true, path);
        }

        [NotNull]
        private static IEnumerable<String> GetChunks([CanBeNull] String url, RegexRule regexRule)
        {
            if (url == null)
                return new List<String>();

            // If each_segment is false, just return the whole URL as one chunk
            if (!regexRule.EachSegment)
                return new List<String> { url };

            // Otherwise return each segment as a chunk
            return url.Split(MetricNames.PathSeparatorChar);
        }

        [NotNull, Pure]
        private static IEnumerable<String> ApplyRuleToChunks(RegexRule regexRule, [CanBeNull] IEnumerable<String> chunks, out Boolean anyChunkUpdated)
        {
            if (chunks == null)
            {
                anyChunkUpdated = false;
                return new List<String>();
            }

            var wasChunkUpdated = false;

            var updatedChunks = chunks
                .Where(chunk => chunk != null)
                .Select(chunk =>
                {
                    var result = ApplyRuleToChunk(chunk, regexRule);
                    if (!result.IsMatch)
                        return chunk;

                    wasChunkUpdated = true;
                    return result.Replacement;
                })
                .ToList();

            anyChunkUpdated = wasChunkUpdated;
            return updatedChunks;
        }

        private static RuleResult ApplyRuleToChunk([CanBeNull] String chunk, RegexRule regexRule)
        {
            if (String.IsNullOrEmpty(chunk) || !regexRule.MatchRegex.IsMatch(chunk))
                return RuleResult.NoMatch;

            if (regexRule.Ignore)
                return RuleResult.Ignore;

            if (regexRule.Replacement == null)
                throw new Exception("Replacement string was null for non-ignore rule");

            var replacement = regexRule.ReplaceAll ?
                regexRule.MatchRegex.Replace(chunk, regexRule.Replacement) :
                regexRule.MatchRegex.Replace(chunk, regexRule.Replacement, 1);

            return new RuleResult(true, replacement);
        }

        public struct RuleResult
        {
            public readonly static RuleResult NoMatch = new RuleResult(false, null);
            public readonly static RuleResult Ignore = new RuleResult(true, null);

            public readonly Boolean IsMatch;

            [CanBeNull]
            public readonly String Replacement;

            public RuleResult(Boolean isMatch, [CanBeNull] String replacement)
            {
                IsMatch = isMatch;
                Replacement = replacement;
            }
        }
    }
}
