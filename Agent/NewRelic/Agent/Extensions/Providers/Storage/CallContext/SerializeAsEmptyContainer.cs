using System;

namespace NewRelic.Providers.Storage.CallContext
{
	public class MarshalByRefContainer : MarshalByRefObject
	{
		private object _value;

		public object GetValue()
		{
			return _value;
		}

		public void SetValue(object value)
		{
			_value = value;
		}

		public MarshalByRefContainer(object instance)
		{
			SetValue(instance);
		}

		public MarshalByRefContainer()
		{
		}
	}
}
