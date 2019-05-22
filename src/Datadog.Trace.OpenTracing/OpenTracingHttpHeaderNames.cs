namespace Datadog.Trace.OpenTracing
{
    /// <summary>
    /// Open Tracing related http header names
    /// </summary>
    public static class OpenTracingHttpHeaderNames
    {
        /// <summary>
        /// Http header suffix for propagated baggage items;
        /// </summary>
        public const string BaggagePrefix = "x-datadog-baggage-";
    }
}
