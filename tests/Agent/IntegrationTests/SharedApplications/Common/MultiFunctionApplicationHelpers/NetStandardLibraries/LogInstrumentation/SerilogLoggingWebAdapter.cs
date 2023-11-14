// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NET7_0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.LogInstrumentation
{
    class SerilogLoggingWebAdapter : ILoggingAdapter
    {
        private static readonly HttpClient _client = new HttpClient();
        private readonly string _loggingPort;
        private string _uriBase;

        public SerilogLoggingWebAdapter(string loggingPort)
        {
            _loggingPort = loggingPort;
        }

        public void Debug(string message)
        {
            _ = _client.GetStringAsync(_uriBase + "test?logLevel=DEBUG&message=" + message).Result;
        }

        public void Info(string message)
        {
            _ = _client.GetStringAsync(_uriBase + "test?logLevel=INFO&message=" + message).Result;
        }
        public void Info(string message, Dictionary<string, object> context)
        {
            var contextString = string.Join(", ", context.Select(c => c.Key + "=" + c.Value));

            _ = _client.GetStringAsync(_uriBase + "testContext?message=" + message + "&contextData=" + contextString).Result;
        }

        public void InfoWithParam(string message, object param)
        {
            throw new NotImplementedException();
        }

        public void Warn(string message)
        {
            _ = _client.GetStringAsync(_uriBase + "test?logLevel=WARN&message=" + message).Result;
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
            _ = _client.GetStringAsync(_uriBase + "test?logLevel=NOMESSAGE&message=" + string.Empty).Result;
        }

        public void Fatal(string message)
        {
            _ = _client.GetStringAsync(_uriBase + "test?logLevel=FATAL&message=" + message).Result;
        }

        public void NoMessage()
        {
            _ = _client.GetStringAsync(_uriBase + "test?logLevel=FATAL&message=EMPTY").Result;
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

            _uriBase = "http://127.0.0.1:" + _loggingPort + "/";
            var hostTask = CreateHostBuilder(_uriBase).Build().RunAsync();

            Console.WriteLine("URI: " + _uriBase);

            // builder
            IHostBuilder CreateHostBuilder(string uriBase)
            {
                return Host.CreateDefaultBuilder()
                .UseSerilog(logger)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                webBuilder.UseStartup<SerilogWebStartup>();
                webBuilder.UseUrls(uriBase);
                });
            }
        }

        public void ConfigurePatternLayoutAppenderForDecoration()
        {
            var loggerConfig = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .MinimumLevel.Information()
            .WriteTo.Console(
                outputTemplate: "ThisIsAWebLog {Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {NR_LINKING} {NewLine}{Exception}"
            );
         
            RunApplication(loggerConfig);
        }

        public void ConfigureJsonLayoutAppenderForDecoration()
        {
            throw new NotImplementedException();
        }

    }

    public class SerilogWebStartup
    {
        public SerilogWebStartup(IConfiguration configuration)
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
