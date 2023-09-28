using System;
using NewRelic.Providers.Wrapper.AspNetCore.BrowserInjection;

namespace NewRelic.Providers.Wrapper.AspNetCore.BrowserInjection
{
    public static class ArrayExtensions
    {
        public static int LastIndexOf<T>(this T[] array, T[] sought) where T : IEquatable<T> =>
            array.AsSpan().LastIndexOf(sought);
    }
}