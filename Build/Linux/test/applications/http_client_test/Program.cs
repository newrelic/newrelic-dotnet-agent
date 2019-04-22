using System;

namespace http_client_test
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            var httpClient = new System.Net.Http.HttpClient();
            var response = System.Threading.Tasks.Task.Run(() => httpClient.GetAsync("http://www.google.com")).Result;
 
            //will throw an exception if not successful
            response.EnsureSuccessStatusCode();
        }
    }
}
