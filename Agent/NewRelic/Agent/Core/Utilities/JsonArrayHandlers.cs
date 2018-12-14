using Newtonsoft.Json;
using System.IO;

namespace NewRelic.Agent.Core.Utilities
{
	/// <summary>
	/// This class exists to replace the use of JsonArrayConverter (and other Json.Convert types) with manual serialize and deserialize methods.
	/// This is being done due to the large performance impact JsonArrayConverter has on the agent in terms of CPU, Memory, GC, and Time intensive.
	/// </summary>
	public class JsonArrayHandlers
	{
		/// <summary>
		/// Responsible for performing a more performant json parse into an array.  Heavily influenced by CAT Headers implementation.
		/// Accepts expected minimum and maximum sizes and returns null in most cases if the JSON cannot be converted to an array.
		/// </summary>
		/// <param name="json"></param>
		/// <param name="minimumTokens">The minimum number of tokens in the array.  If less than this, return null.</param>
		/// <param name="maximumTokens">The maximum number of array items that can be in the json array.  The returned array is always this size.</param>
		/// <returns>An array containing the string from the JSON.</returns>
		public static string[] ConvertJsonToStringArrayForCat(string json, int minimumTokens, int maximumTokens)
		{
			using (var reader = new JsonTextReader(new StringReader(json)))
			{
				// confirms this is an array
				if (!(reader.Read() && reader.TokenType == JsonToken.StartArray))
				{
					return null;
				}

				var arrayIndex = 0;
				var stringArray = new string[maximumTokens];

				while (arrayIndex < maximumTokens && reader.Read() && reader.TokenType != JsonToken.EndArray)
				{

					switch (reader.TokenType)
					{
						case JsonToken.Integer:
						case JsonToken.Float:
						case JsonToken.String:
						case JsonToken.Boolean:
							stringArray[arrayIndex] = reader.Value.ToString();
							break;

						case JsonToken.Null:
							stringArray[arrayIndex] = null;
							break;
						
						// The types not on the list should return a null since that indicates a major problem
						default:
							return null;
					}

					++arrayIndex;
				}

				// this means we have too few tokens
				if (arrayIndex < minimumTokens)
				{
					return null;
				}

				// this means that we have exhausted the reader, but did not encounter the array close, invalid json array
				if(arrayIndex < maximumTokens && reader.TokenType != JsonToken.EndArray)
				{
					return null;
				}

				// If we are at max tokens but there is still more to read, we will overflow our array which indicates a problem as well
				if (arrayIndex == maximumTokens && reader.Read() && reader.TokenType != JsonToken.EndArray)
				{
					return null;
				}

				return stringArray;
			}
		}
	}
}
