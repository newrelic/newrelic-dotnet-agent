using System.Threading;

namespace NewRelic.Collections
{

	public struct StaticCounter
	{
		private static long _value = 0;
		public static long Value => Interlocked.Read(ref _value);

		public static long Next()
		{
			return Interlocked.Increment(ref _value);
		}

		public static long Reset()
		{
			return Interlocked.Exchange(ref _value, 0);
		}
	}
}