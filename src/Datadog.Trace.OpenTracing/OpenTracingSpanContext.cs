using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using Datadog.Trace.Logging;

namespace Datadog.Trace.OpenTracing
{
    internal class OpenTracingSpanContext : global::OpenTracing.ISpanContext
    {
        private static ILog _log = LogProvider.For<OpenTracingSpanContext>();

        public OpenTracingSpanContext(ISpanContext context, IEnumerable<KeyValuePair<string, string>> baggage = null)
        {
            Context = context;
            Baggage = baggage != null ? new ConcurrentDictionary<string, string>(baggage) : new ConcurrentDictionary<string, string>();
        }

        public string TraceId => Context.TraceId.ToString(CultureInfo.InvariantCulture);

        public string SpanId => Context.SpanId.ToString(CultureInfo.InvariantCulture);

        internal ISpanContext Context { get; }

        private ConcurrentDictionary<string, string> Baggage { get; }

        public IEnumerable<KeyValuePair<string, string>> GetBaggageItems()
        {
            return Baggage.ToArray();
        }

        public string GetBaggageItem(string key)
        {
            return Baggage.TryGetValue(key, out var value) ? value : null;
        }

        public void SetBaggageItem(string key, string value)
        {
            Baggage.AddOrUpdate(key, value, (k, cmp) => value);
        }
    }
}
