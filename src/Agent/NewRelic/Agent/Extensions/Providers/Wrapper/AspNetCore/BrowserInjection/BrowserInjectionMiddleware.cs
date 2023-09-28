using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace NewRelic.Providers.Wrapper.AspNetCore
{
    internal class BrowserInjectionMiddleware
    {
        private readonly RequestDelegate _next;

        public BrowserInjectionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            using var injectedResponse = new ResponseStreamWrapper(context.Response.Body, context);
            context.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(injectedResponse));

            await _next(context);
        }
    }
}