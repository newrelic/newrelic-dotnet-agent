// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NET6_0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NewRelic.Agent.IntegrationTestHelpers;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.LogInstrumentation
{
    class NLogLoggingWebAdapter : ILoggingAdapter
    {
        private static readonly HttpClient _client = new HttpClient();
        private string _uriBase;

        public NLogLoggingWebAdapter()
        {
        }

        public void Debug(string message)
        {
            var result = _client.GetStringAsync(_uriBase + "test?logLevel=DEBUG&message=" + message).Result;
        }

        public void Info(string message)
        {
            var result = _client.GetStringAsync(_uriBase + "test?logLevel=INFO&message=" + message).Result;
        }
        public void Info(string message, Dictionary<string, object> context)
        {
            var contextString = string.Join(", ", context.Select(c => c.Key + "=" + c.Value));

            var result = _client.GetStringAsync(_uriBase + "testContext?message=" + message + "&contextData=" + contextString).Result;
        }


        public void Warn(string message)
        {
            var result = _client.GetStringAsync(_uriBase + "test?logLevel=WARN&message=" + message).Result;
        }

        public void Error(Exception exception)
        {
            // In this case we are not passing the exact Exception to the test app, just the message.
            // The test app will create an Exception for us.
            // As long as it has the same message and class with a stacktrace of any kind, its good for test.
            var result = _client.GetStringAsync(_uriBase + "test?logLevel=ERROR&message=" + exception.Message).Result;
        }

        public void ErrorNoMessage(Exception exception)
        {
            // In this case we are not passing the exact Exception to the test app, just the message.
            // The test app will create an Exception for us.
            // As long as it has the same message and class with a stacktrace of any kind, its good for test.
            var result = _client.GetStringAsync(_uriBase + "test?logLevel=NOMESSAGE&message=" + string.Empty).Result;
        }

        public void Fatal(string message)
        {
            var result = _client.GetStringAsync(_uriBase + "test?logLevel=FATAL&message=" + message).Result;
        }

        public void NoMessage()
        {
            var result = _client.GetStringAsync(_uriBase + "test?logLevel=FATAL&message=EMPTY").Result;
        }

        public void Configure()
        {

            var loggerConfig = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Fatal)
                .MinimumLevel.Debug()
                .WriteTo.Console();

            RunApplication(loggerConfig);
        }

        public void ConfigureWithInfoLevelEnabled()
        {
            var loggerConfig = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Fatal)
                .MinimumLevel.Information()
                .WriteTo.Console();

            RunApplication(loggerConfig);
        }

        private void RunApplication(LoggerConfiguration loggerConfig)
        {
            var logger = loggerConfig.CreateLogger();

            var port = RandomPortGenerator.NextPort();
            _uriBase = "http://localhost:" + port + "/";
            var hostTask = CreateHostBuilder(_uriBase).Build().RunAsync();

            Console.WriteLine("URI: " + _uriBase);

            // builder
            IHostBuilder CreateHostBuilder(string uriBase)
            {
                var builder = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<NLogWebStartup>();
                    webBuilder.UseUrls(uriBase);
                });

                return builder;
            }
        }

        public void ConfigurePatternLayoutAppenderForDecoration()
        {
            var loggerConfig = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .MinimumLevel.Information()
            .WriteTo.Console(
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {NR_LINKING} {NewLine}{Exception}"
            );

            RunApplication(loggerConfig);
        }

        public void ConfigureJsonLayoutAppenderForDecoration()
        {
            throw new NotImplementedException();
        }

    }

    public class NLogWebStartup
    {
        public NLogWebStartup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}

#endif
