using System;

namespace NewRelic.Agent
{
	[Flags]
	public enum AttributeDestinations : byte
	{
		None = 0,
		TransactionTrace = 1 << 0,
		TransactionEvent = 1 << 1,
		ErrorTrace = 1 << 2,
		JavaScriptAgent = 1 << 3,
		ErrorEvent = 1 << 4,
		SqlTrace = 1 << 5,
		All = 0xFF,
	}
}
