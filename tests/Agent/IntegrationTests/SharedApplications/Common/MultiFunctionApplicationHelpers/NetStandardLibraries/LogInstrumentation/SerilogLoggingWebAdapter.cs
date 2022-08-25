// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NET6_0

using System;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NewRelic.Agent.IntegrationTestHelpers;
using Serilog;
using Serilog.Events;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.LogInstrumentation
{
    class SerilogLoggingWebAdapter : ILoggingAdapter
    {
        private HttpClient _client;
        private string _uriBase;

        public SerilogLoggingWebAdapter()
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

        public void Fatal(string message)
        {
            var result = _client.GetStringAsync(_uriBase + "test?logLevel=FATAL&message=" + message).Result;
        }

        public void Configure()
        {
            _client = new HttpClient();

            var loggerConfig = new LoggerConfiguration();

            loggerConfig
                .Enrich.FromLogContext()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Fatal)
                .MinimumLevel.Debug()
                .WriteTo.Console();

            Log.Logger = loggerConfig.CreateLogger();

            var port = RandomPortGenerator.NextPort();
            _uriBase = "http://localhost:" + port + "/";
            var hostTask = CreateHostBuilder(_uriBase).Build().RunAsync();

            Console.WriteLine("URI: " + _uriBase);

            // builder
            IHostBuilder CreateHostBuilder(string uriBase)
            {
                return Host.CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                webBuilder.UseStartup<Startup>();
                webBuilder.UseUrls(uriBase);
                });
            }
        }

        public void ConfigurePatternLayoutAppenderForDecoration()
        {
            throw new NotImplementedException();
        }

        public void ConfigureJsonLayoutAppenderForDecoration()
        {
            throw new NotImplementedException();
        }
    }

    public class Startup
    {
        public Startup(IConfiguration configuration)
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

            app.UseSerilogRequestLogging();
        }
    }
}

#endif
