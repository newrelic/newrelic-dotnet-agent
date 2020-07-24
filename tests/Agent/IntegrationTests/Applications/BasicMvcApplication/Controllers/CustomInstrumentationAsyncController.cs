using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Mvc;
using JetBrains.Annotations;

namespace BasicMvcApplication.Controllers
{
    public class CustomInstrumentationAsyncController : Controller
    {
        [HttpGet]
        public async Task<string> AsyncGet()
        {
            await CustomSegmentTransactionSegmentWrapper("AsyncCustomSegmentName");
            await CustomSegmentAlternateParameterNamingTheSegment(10, "AsyncCustomSegmentNameAlternate");

            var result = await CustomMethodDefaultWrapperAsync();
            return result;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task<string> CustomMethodDefaultWrapperAsync()
        {
            return await Task.FromResult("Async is working");
        }

        // Tests an ordinary async background transaction
        [HttpGet]
        public async Task<string> GetBackgroundThread()
        {
            //This is to make sure that only custom transaction will be generated.
            NewRelic.Api.Agent.NewRelic.IgnoreTransaction();

            // Intentionally run this method on a background thread so it won't be caught up as part of the current transaction.
            // Don't wait for it to finish or else it won't be able to generate a transaction trace (because it will be eclipsed by the request)
            await Task.Run(async () => await CustomMethodBackgroundThread());

            return "Async is working";
        }

        [NotNull]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task CustomMethodBackgroundThread()
        {
            // Force a trace to be generated
            await Task.Delay(TimeSpan.FromMilliseconds(1));
        }

        // Tests an async background transaction where an exception occurs immediately (before any awaits)
        [HttpGet]
        public async Task<string> GetBackgroundThreadWithError()
        {
            //This is to make sure that only custom transaction will be generated.
            NewRelic.Api.Agent.NewRelic.IgnoreTransaction();

            // Intentionally run this method on a background thread so it won't be caught up as part of the current transaction.
            // Don't wait for it to finish or else it won't be able to generate a transaction trace (because it will be eclipsed by the request)
            await Task.Run(async () => await CustomMethodBackgroundThreadWithError());

            return "Async is working";
        }


        [NotNull]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task CustomMethodBackgroundThreadWithError()
        {
            await Task.Delay(1);
            throw new Exception("oh no!");
        }


        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task<string> CustomSegmentTransactionSegmentWrapper(string segmentName)
        {
            var str = await Task.Run(() => JustSleepAndReturnParamString(segmentName));
            return str;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task<string> CustomSegmentAlternateParameterNamingTheSegment(int x, string segmentName)
        {
            x++;
            var str = await Task.Run(() => JustSleepAndReturnParamString(segmentName));
            return str;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string JustSleepAndReturnParamString(string s)
        {
            Thread.Sleep(TimeSpan.FromMilliseconds(5));
            return s;
        }
    }
}
