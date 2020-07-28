using System.Text.RegularExpressions;

namespace NewRelic.Agent.Configuration
{
    public struct RegexRule
    {
        public readonly string MatchExpression;
        public readonly string Replacement;
        public readonly bool Ignore;
        public readonly long EvaluationOrder;
        public readonly bool TerminateChain;
        public readonly bool EachSegment;
        public readonly bool ReplaceAll;
        public readonly Regex MatchRegex;

        public RegexRule(string matchExpression, string replacement, bool ignore, long evaluationOrder, bool terminateChain, bool eachSegment, bool replaceAll)
        {
            MatchExpression = matchExpression;
            Replacement = replacement;
            Ignore = ignore;
            EvaluationOrder = evaluationOrder;
            TerminateChain = terminateChain;
            EachSegment = eachSegment;
            ReplaceAll = replaceAll;
            MatchRegex = new Regex(MatchExpression, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        }
    }
}
