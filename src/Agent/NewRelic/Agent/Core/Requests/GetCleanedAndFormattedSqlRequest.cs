using System;

namespace NewRelic.Agent.Core.Requests
{
    public class GetCleanedAndFormattedSqlRequest
    {
        public readonly String SqlStatement;

        public GetCleanedAndFormattedSqlRequest(String sqlStatement)
        {
            if (sqlStatement == null) throw new ArgumentNullException("sqlStatement");
            SqlStatement = sqlStatement;
        }
    }
}
