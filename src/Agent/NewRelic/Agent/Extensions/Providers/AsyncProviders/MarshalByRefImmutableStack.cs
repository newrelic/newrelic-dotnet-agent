using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace NewRelic.Providers.AsyncProviders
{
	/// <summary>
	/// A thin wrapper around ImmutableStack that implements ISerialize and returns null when serialized. This type is used to allow us to store data inside CallContext and meet the serializability contract. We don't need to make this data truly serializable because the only time the stack is ever serialized is when we are crossing an app domain boundary, in which case we don't care about having the stack come out on the other end.
	/// </summary>
	[Serializable]
	public class MarshalByRefImmutableStack<T> : MarshalByRefObject, IImmutableStack<T>
	{
		public static readonly MarshalByRefImmutableStack<T> Empty = new MarshalByRefImmutableStack<T>();

		private readonly IImmutableStack<T> _stack;

		public MarshalByRefImmutableStack(IImmutableStack<T> stack)
		{
			_stack = stack ?? ImmutableStack<T>.Empty;
		}

		public MarshalByRefImmutableStack()
		{
			_stack = ImmutableStack<T>.Empty;
		}

		public MarshalByRefImmutableStack<T> Clear()
		{
			return new MarshalByRefImmutableStack<T>(_stack.Clear());
		}

		public MarshalByRefImmutableStack<T> Push(T value)
		{
			return new MarshalByRefImmutableStack<T>(_stack.Push(value));
		}

		public MarshalByRefImmutableStack<T> Pop()
		{
			return new MarshalByRefImmutableStack<T>(_stack.Pop());
		}

		#region IImmutableStack<T> implementations

		public IEnumerator GetEnumerator()
		{
			return _stack.GetEnumerator();
		}

		IImmutableStack<T> IImmutableStack<T>.Clear()
		{
			return Clear();
		}

		IImmutableStack<T> IImmutableStack<T>.Push(T value)
		{
			return Push(value);
		}

		IImmutableStack<T> IImmutableStack<T>.Pop()
		{
			return Pop();
		}

		IEnumerator<T> IEnumerable<T>.GetEnumerator()
		{
			return _stack.GetEnumerator();
		}

		public T Peek()
		{
			return _stack.Peek();
		}

		public Boolean IsEmpty => _stack.IsEmpty;

		#endregion IImmutableStack<T> implementations
	}
}