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
        public int Id { get; private set; }
        public string FooBar { get; private set; }
        public bool IsValid { get; private set; }

        public SampleNServiceBusMessage2(int id, string fooBar, bool isValid = true)
        {
            Thread.Sleep(250);
            Id = id;
            FooBar = fooBar;
            IsValid = isValid;
        }
    }
}
