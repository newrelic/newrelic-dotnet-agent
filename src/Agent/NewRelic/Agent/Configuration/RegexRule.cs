using System;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace NewRelic.Agent.Configuration
{
	public struct RegexRule
	{
		[NotNull]
		public readonly String MatchExpression;
		[CanBeNull]
		public readonly String Replacement;
		public readonly Boolean Ignore;
		public readonly Int64 EvaluationOrder;
		public readonly Boolean TerminateChain;
		public readonly Boolean EachSegment;
		public readonly Boolean ReplaceAll;
		[NotNull]
		public readonly Regex MatchRegex;

		public RegexRule([NotNull] String matchExpression, [CanBeNull] String replacement, Boolean ignore, Int64 evaluationOrder, Boolean terminateChain, Boolean eachSegment, Boolean replaceAll)
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
