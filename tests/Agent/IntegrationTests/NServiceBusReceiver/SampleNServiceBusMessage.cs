using System;
using System.Threading;
using NServiceBus;

namespace NServiceBusReceiver

{
	public class SampleNServiceBusMessage : ICommand
	{
		public Int32 Id { get; private set; }
		public String FooBar { get; private set; }

		public SampleNServiceBusMessage(Int32 id, String fooBar)
		{
			Thread.Sleep(250);
			Id = id;
			FooBar = fooBar;
		}
	}
}
