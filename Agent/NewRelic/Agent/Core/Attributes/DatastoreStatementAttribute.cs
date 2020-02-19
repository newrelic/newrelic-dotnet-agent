using System.Text;

namespace NewRelic.Agent.Core.Attributes
{
	/// <summary>
	/// Specialized Attribute that suports database statements which may exceed the 
	/// https://source.datanerd.us/agents/agent-specs/blob/master/Span-Events.md#dbstatement
	/// </summary>
	public class DatastoreStatementAttribute : Attribute<string>
	{
		protected const int DATA_STORE_STATEMENT_LENGTH_LIMIT = 1999;  //bytes
		protected const string ELIPSES = "..."; 

		//public DatastoreStatementAttribute(AttributeDestinations defaultDestinations, string key, string value)
		//	 : base(key, TruncateDatastoreStatement(value, DATA_STORE_STATEMENT_LENGTH_LIMIT), defaultDestinations)
		//{
		//}

		public DatastoreStatementAttribute(string key, string value, AttributeClassification classification, AttributeDestinations defaultDestinations)
			: base(key, TruncateDatastoreStatement(value, DATA_STORE_STATEMENT_LENGTH_LIMIT), classification, defaultDestinations)
		{
		}


		/// <summary>
		/// Truncates the statement from a Datastore segment to 2000 bytes or less.  This occurs post-obfuscation.
		/// </summary>
		/// <param name="statement">Obfuscated statement from datastore segment.</param>
		/// <returns>Truncated statement.</returns>
		private static string TruncateDatastoreStatement(string statement, int maxSizeBytes)
		{
			const int maxBytesPerUtf8Char = 4;
			const byte firstByte = 0b11000000;
			const byte highBit = 0b10000000;

			var maxCharactersWillFitWithoutTruncation = maxSizeBytes / maxBytesPerUtf8Char;
		
			if (statement.Length <= maxCharactersWillFitWithoutTruncation)
			{
				return statement;
			}

			var byteArray = Encoding.UTF8.GetBytes(statement);

			if (byteArray.Length <= maxSizeBytes)
			{
				return statement;
			}

			var actualMaxStatementLength = maxSizeBytes - 3;

			var byteOffset = actualMaxStatementLength;

			// Check high bit to see if we're [potentially] in the middle of a multi-byte char
			if ((byteArray[byteOffset] & highBit) == highBit)
			{
				// If so, keep walking back until we have a byte starting with `11`,
				// which means the first byte of a multi-byte UTF8 character.
				while (firstByte != (byteArray[byteOffset] & firstByte))
				{
					byteOffset--;
				}
			}

			return Encoding.UTF8.GetString(byteArray, 0, byteOffset) + "...";
		}



	}
}