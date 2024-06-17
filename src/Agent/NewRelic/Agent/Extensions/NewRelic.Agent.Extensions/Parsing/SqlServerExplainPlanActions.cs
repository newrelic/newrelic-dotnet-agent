// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Extensions.Parsing
{
    public static class SqlServerExplainPlanActions
    {
        public static ExplainPlan GenerateExplainPlan(object resources)
        {
            if (!(resources is IDbCommand))
            {
                return null;
            }

            ExplainPlan explainPlan = null;

            //KILL THE CONNECTION NO MATTER WHAT HAPPENS DURING EXECUTION TO AVOID CONNECTION LEAKING
            var dbCommand = (IDbCommand)resources;
            using (dbCommand)
            using (dbCommand.Connection)
            {
                var shouldGeneratePlan = SqlParser.FixParameterizedSql(dbCommand);
                if (!shouldGeneratePlan)
                {
                    return explainPlan;
                }

                if (dbCommand.Connection.State != ConnectionState.Open)
                {
                    dbCommand.Connection.Open();
                }

                SetShowPlan(dbCommand.Connection, true);

                using (IDataReader reader = dbCommand.ExecuteReader())
                {
                    var headers = GetReaderHeaders(reader);
                    // Decide which headers should be obfuscated based on the Vendor (this is only SQL)
                    var obfuscatedHeaders = GetObfuscatedIndexes(headers);
                    explainPlan = new ExplainPlan(headers, new List<List<object>>(), obfuscatedHeaders);

                    var explainPlanDatas = new List<List<object>>();
                    while (reader.Read())
                    {
                        object[] values = new object[reader.FieldCount];
                        reader.GetValues(values);
                        explainPlanDatas.Add(values.ToList());
                    }

                    explainPlan.ExplainPlanDatas = explainPlanDatas;

                }

                SetShowPlan(dbCommand.Connection, false);
            }

            return explainPlan;
        }

        public static object AllocateResources(IDbCommand command)
        {
            if (!(command is ICloneable))
                return null;

            var clonedCommand = (IDbCommand)((ICloneable)command).Clone();
            var connection = (IDbConnection)((ICloneable)command.Connection).Clone();

            clonedCommand.Connection = connection;
            clonedCommand.Transaction = null;

            return clonedCommand;
        }

        public static List<int> GetObfuscatedIndexes(List<string> headers)
        {
            var indexes = new List<int>(ObfuscateFieldNames().Length);
            foreach (var field in ObfuscateFieldNames())
            {
                var index = headers.FindIndex(field.Equals);
                if (index >= 0)
                {
                    indexes.Add(index);
                }
            }

            return indexes;
        }

        private static List<string> GetReaderHeaders(IDataReader reader)
        {
            List<string> headers = new List<string>(reader.FieldCount);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                headers.Add(reader.GetName(i));
            }
            return headers;
        }

        private static string[] ObfuscateFieldNames()
        {
            return new[] {
                    "StmtText",
                    "Argument"
                };
        }

        private static void SetShowPlan(IDbConnection connection, bool on)
        {
            using (IDbCommand command = connection.CreateCommand())
            {
                command.CommandText = "SET SHOWPLAN_ALL " + (on ? "ON" : "OFF");
                command.ExecuteNonQuery();
            }
        }
    }
}
