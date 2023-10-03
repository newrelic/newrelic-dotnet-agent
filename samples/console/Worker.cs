using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NewRelic.Api.Agent;

namespace ConsoleSample;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly HttpClient _httpClient;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Worker service is executing");
            while (!stoppingToken.IsCancellationRequested)
            {
                await GetTheWeatherAsync();

                _logger.LogInformation("Chilling for a few seconds...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
        finally
        {
            _logger.LogInformation("Worker service is terminating");
        }
    }

    public override void Dispose()
    {
        _httpClient.Dispose();
    }

    /// <summary>
    /// Call an open API service to get the weather for Portland, OR.
    /// Using the [Transaction] attribute ensures that this method is instrumented by the .NET Agent
    /// </summary>
    /// <returns></returns>
    [Transaction]
    private async Task GetTheWeatherAsync()
    {
        try
        {
            _logger.LogInformation("Getting the weather forecast for Portland, OR");
            var response = await _httpClient.GetStringAsync(
                "https://api.open-meteo.com/v1/forecast?latitude=45.5234&longitude=-122.6762&hourly=temperature_2m&temperature_unit=fahrenheit&windspeed_unit=mph&precipitation_unit=inch&timezone=America%2FLos_Angeles&forecast_days=1");

            _logger.LogInformation("Here's the forecast: {Forecast}", response);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unexpected exception calling HttpClient.GetStringAsync()");
        }
    }
}
