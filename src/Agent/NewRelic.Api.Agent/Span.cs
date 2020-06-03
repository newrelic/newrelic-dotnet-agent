using Microsoft.CSharp.RuntimeBinder;

namespace NewRelic.Api.Agent
{
    internal class Span : ISpan
    {
        private readonly dynamic _wrappedSpan;
        private static ISpan _noOpSpan = new NoOpSpan();

        internal Span(dynamic wrappedSpan = null)
        {
            _wrappedSpan = wrappedSpan ?? _noOpSpan;
        }

        private static bool _isAddCustomAttributeAvailable = true;

        public ISpan AddCustomAttribute(string key, object value)
        {
            if (!_isAddCustomAttributeAvailable)
            {
                return _noOpSpan.AddCustomAttribute(key, value);
            }

            try
            {
                _wrappedSpan.AddCustomAttribute(key, value);
                return this;
            }
            catch (RuntimeBinderException ex)
            {
                _isAddCustomAttributeAvailable = false;
            }

            return this;
        }
    }
}
