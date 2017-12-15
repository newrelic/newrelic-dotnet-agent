using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Parsing
{
	public class MySqlExplainPlanActions
	{
		public static ExplainPlan GenerateExplainPlan(Object resources)
		{
			if (!(resources is IDbCommand))
				return null;

			var dbCommand = (IDbCommand)resources;
			if (dbCommand.Connection.State != ConnectionState.Open)
				dbCommand.Connection.Open();

			ExplainPlan explainPlan = null;

			//KILL THE CONNECTION NO MATTER WHAT HAPPENS DURING EXECUTION TO AVOID CONNECTION LEAKING
			using (dbCommand)
			using (dbCommand.Connection)
			{

				dbCommand.Transaction = null;
				SqlParser.FixParameterizedSql(dbCommand);
				dbCommand.CommandText = "EXPLAIN " + dbCommand.CommandText;


				using (IDataReader reader = dbCommand.ExecuteReader())
				{
					var headers = GetReaderHeaders(reader);
					// Decide which headers should be obfuscated based on the Vendor (this is only SQL)
					var obfuscatedHeaders = GetObfuscatedIndexes(headers);
					explainPlan = new ExplainPlan(headers, new List<List<Object>>(), obfuscatedHeaders);

					var explainPlanDatas = new List<List<Object>>();
					while (reader.Read())
					{
						Object[] values = new Object[reader.FieldCount];
						reader.GetValues(values);
						explainPlanDatas.Add(values.ToList());
					}

					explainPlan.ExplainPlanDatas = explainPlanDatas;

				}

			}
			
			return explainPlan;
		}

		public static Object AllocateResources(IDbCommand command)
		{
			if (!(command is ICloneable))
				return null;

			var clonedCommand = (IDbCommand)((ICloneable)command).Clone();
			var connection = (IDbConnection)((ICloneable)command.Connection).Clone();

			clonedCommand.Connection = connection;
			clonedCommand.Transaction = null;

			return clonedCommand;
		}

		public static List<Int32> GetObfuscatedIndexes(List<String> headers)
		{
			var indexes = new List<Int32>(ObfuscateFieldNames().Length);
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

		private static List<String> GetReaderHeaders(IDataReader reader)
		{
			List<String> headers = new List<String>(reader.FieldCount);
			for (Int32 i = 0; i < reader.FieldCount; i++)
			{
				headers.Add(reader.GetName(i));
			}
			return headers;
		}

		private static String[] ObfuscateFieldNames()
		{
			return new String[0];
		}
	}
}
