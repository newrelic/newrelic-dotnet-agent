using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace NewRelic.Providers.Wrapper.AspNetCore.BrowserInjection
{
    internal static class BrowserScriptInjectionHelper
    {
        // could also use </body> or some other tag
        private const string HeadCloseTag = "</head>";
        private static readonly byte[] _headCloseTagBytes = Encoding.UTF8.GetBytes(HeadCloseTag);

        public static Task InjectBrowserScriptAsync(ReadOnlyMemory<byte> buffer, HttpContext context, Stream baseStream)
        {
            return InjectBrowserScriptAsync(buffer.ToArray(), context, baseStream);
        }

        /// <summary>
        /// Injects the script just before the </head> tag
        /// </summary>
        public static async Task InjectBrowserScriptAsync(byte[] buffer, HttpContext context, Stream baseStream)
        {
            var index = buffer.LastIndexOf(_headCloseTagBytes);

            if (index == -1)
            {
                // not found, can't inject anything
                await baseStream.WriteAsync(buffer, 0, buffer.Length);
                return;
            }

            // Write everything before the </head> tag
            await baseStream.WriteAsync(buffer, 0, index - 1);

            // Write the injected script
            var scriptBytes = Encoding.UTF8.GetBytes(GetBrowserScript());
            await baseStream.WriteAsync(scriptBytes, 0, scriptBytes.Length);

            // Write the rest of the doc, starting at the </head> tag
            await baseStream.WriteAsync(buffer, index, buffer.Length - index);
        }

        private static string GetBrowserScript()
        {
            // This would obviously be replaced with the RUM script or a call to appropriate core methods (like BrowserMonitoringScriptMaker?)
            return "<script>console.log('hello world')</script>";
        }
    }
}
