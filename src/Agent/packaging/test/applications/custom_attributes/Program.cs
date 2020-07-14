using System;

namespace custom_attributes
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Invoking custom instrumentation");

            Transaction();
        }

        [NewRelic.Api.Agent.Transaction]
        static void Transaction() 
        {
            Thing();
        }

        [NewRelic.Api.Agent.Trace]
        static void Thing()
        {
        }
    }
}
