using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;

namespace NServiceBusReceiver
{
	public class SampleNServiceBusMessage2 : ICommand
	{
		public Int32 Id { get; private set; }
		public String FooBar { get; private set; }
		public Boolean IsValid { get; private set; }

		public SampleNServiceBusMessage2(Int32 id, String fooBar, Boolean isValid=true)
		{
			Thread.Sleep(250);
			Id = id;
			FooBar = fooBar;
			IsValid = isValid;
		}
	}
}
