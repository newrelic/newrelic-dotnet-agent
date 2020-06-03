using System;

namespace custom_xml
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Invoking custom instrumentation");

            Transaction();
        }

        static void Transaction()
        {
            Thing();
        }

        static void Thing()
        {
        }
    }
}
