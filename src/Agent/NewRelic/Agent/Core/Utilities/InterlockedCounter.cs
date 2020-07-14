using System;
using System.Threading;

namespace NewRelic.Agent.Core.Utilities
{
	/// <summary>
	/// A counter that can only be modified in thread-safe ways.
	/// </summary>
	public class InterlockedCounter
	{
		private Int32 _value;
		public Int32 Value => _value;

		public InterlockedCounter(Int32 initialValue = 0)
		{
			_value = initialValue;
		}

		public void Increment()
		{
			Interlocked.Increment(ref _value);
		}

		public void Decrement()
		{
			Interlocked.Decrement(ref _value);
		}

		public void Add(Int32 value)
		{
			Interlocked.Add(ref _value, value);
		}

		public Int32 Exchange(Int32 value)
		{
			return Interlocked.Exchange(ref _value, value);
		}

		public Int32 CompareExchange(Int32 value, Int32 comparand)
		{
			return Interlocked.CompareExchange(ref _value, value, comparand);
		}

		public void Set(Int32 value)
		{
			_value = value;
		}
	}
}
