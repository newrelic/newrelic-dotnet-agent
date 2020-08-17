// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Sinks.SumoLogic;

namespace SerilogSumologicApplication
{
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

            var loggingHandler = new LoggingHandler();

            Log.Logger = new LoggerConfiguration()
                .WriteTo.SumoLogic("https://endpoint3.collection.us2.sumologic.com/receiver/v1/http/ZaVnC4dhaV3RNOE402S4RYn9UbAqGrwPSgZoI_9Cm3dy4JWEwJkAEIVpxcS6gWbCj-Xl2W00HfdMhWtSWDo6vRFfmKNfOWXLztlFKBeyiZhUXeptcmKoVA==", "MiddlewareLogTest", handler: loggingHandler)
                .CreateLogger();

            services.AddMvc();

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseBrowserLink();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();

            app.UseMiddleware<LoggingMiddleware>();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }

    public class LoggingHandler : DelegatingHandler
    {
        public LoggingHandler()
        {
            InnerHandler = new HttpClientHandler();
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Console.WriteLine($"Request: {request}");
            try
            {
                // base.SendAsync calls the inner handler
                var response = await base.SendAsync(request, cancellationToken);
                Console.WriteLine($"Response: {response}");
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to get response: {ex}");
                throw;
            }
        }
    }
}
