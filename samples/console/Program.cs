using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace ConsoleSample;

public class Program
{
    public static async Task Main(string[] args)
    {
        // create an IHost instance and add a hosted service to it
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
                {
                    services.AddHostedService<Worker>();
                })
            .Build();

        var logger = host.Services.GetService(typeof(ILogger<Program>)) as ILogger<Program>;

        // run the hosted service until the application is terminated
        logger!.LogInformation("Hello world!");
        await host.RunAsync();
        logger!.LogInformation("Goodbye world!");
    }
}
