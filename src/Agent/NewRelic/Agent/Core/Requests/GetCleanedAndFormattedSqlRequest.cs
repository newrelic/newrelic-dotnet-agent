using System;
using JetBrains.Annotations;

namespace NewRelic.Agent.Core.Requests
{
    public class GetCleanedAndFormattedSqlRequest
    {
        [NotNull] public readonly String SqlStatement;

        public GetCleanedAndFormattedSqlRequest([NotNull] String sqlStatement)
        {
            if (sqlStatement == null) throw new ArgumentNullException("sqlStatement");
            SqlStatement = sqlStatement;
        }
    }
}
