using System;
using System.Collections.Generic;
using System.Globalization;
using Datadog.Trace.Headers;
using OpenTracing.Propagation;

namespace Datadog.Trace.OpenTracing
{
    internal class HttpHeadersCodec : ICodec
    {
        private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

        public global::OpenTracing.ISpanContext Extract(object carrier)
        {
            var map = carrier as ITextMap;

            if (map == null)
            {
                throw new ArgumentException("Carrier should have type ITextMap", nameof(carrier));
            }

            IHeadersCollection headers = new TextMapHeadersCollection(map);
            var propagationContext = SpanContextPropagator.Instance.Extract(headers);
            var baggage = ExtractBaggage(map);
            return new OpenTracingSpanContext(propagationContext, baggage);
        }

        public void Inject(global::OpenTracing.ISpanContext context, object carrier)
        {
            var map = carrier as ITextMap;

            if (map == null)
            {
                throw new ArgumentException("Carrier should have type ITextMap", nameof(carrier));
            }

            IHeadersCollection headers = new TextMapHeadersCollection(map);
            InjectBaggage(context.GetBaggageItems(), headers);

            if (context is OpenTracingSpanContext otSpanContext && otSpanContext.Context is SpanContext ddSpanContext)
            {
                // this is a Datadog context
                SpanContextPropagator.Instance.Inject(ddSpanContext, headers);
            }
            else
            {
                // any other OpenTracing.ISpanContext
                headers.Set(HttpHeaderNames.TraceId, context.TraceId.ToString(InvariantCulture));
                headers.Set(HttpHeaderNames.ParentId, context.SpanId.ToString(InvariantCulture));
            }
        }

        private void InjectBaggage(IEnumerable<KeyValuePair<string, string>> baggage, IHeadersCollection headers)
        {
            if (baggage == null)
            {
                return;
            }

            foreach (var kv in baggage)
            {
                headers.Set($"{OpenTracingHttpHeaderNames.BaggagePrefix}{kv.Key}", kv.Value);
            }
        }

        private IEnumerable<KeyValuePair<string, string>> ExtractBaggage(ITextMap headers)
        {
            if (headers == null)
            {
                yield break;
            }

            foreach (var kv in headers)
            {
                if (kv.Key.StartsWith(OpenTracingHttpHeaderNames.BaggagePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    yield return new KeyValuePair<string, string>(
                        kv.Key.Substring(OpenTracingHttpHeaderNames.BaggagePrefix.Length),
                        kv.Value);
                }
            }
        }
    }
}
