using System;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers;
using Moq;
using OpenTracing;
using OpenTracing.Propagation;
using Xunit;

namespace Datadog.Trace.OpenTracing.Tests
{
    public class OpenTracingTracerTests
    {
        private readonly OpenTracingTracer _tracer;

        public OpenTracingTracerTests()
        {
            var settings = new TracerSettings();
            var writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ISampler>();

            var datadogTracer = new Tracer(settings, writerMock.Object, samplerMock.Object, null);

            _tracer = new OpenTracingTracer(datadogTracer);
        }

        [Fact]
        public void BuildSpan_NoParameter_DefaultParameters()
        {
            var builder = _tracer.BuildSpan("Op1");
            var span = (OpenTracingSpan)builder.Start();

            Assert.Contains(span.DDSpan.ServiceName, TestRunners.ValidNames);
            Assert.Equal("Op1", span.DDSpan.OperationName);
        }

        [Fact]
        public void BuildSpan_OneChild_ChildParentProperlySet()
        {
            const string baggageKey = "BaggageKey";
            const string baggageValue = "BaggageValue";
            IScope root = _tracer
                         .BuildSpan("Root")
                         .StartActive(finishSpanOnDispose: true);

            root.Span.SetBaggageItem(baggageKey, baggageValue);

            IScope child = _tracer
                          .BuildSpan("Child")
                          .StartActive(finishSpanOnDispose: true);

            Span rootDatadogSpan = ((OpenTracingSpan)root.Span).Span;
            Span childDatadogSpan = ((OpenTracingSpan)child.Span).Span;

            Assert.Equal(rootDatadogSpan.Context.TraceContext, (ITraceContext)childDatadogSpan.Context.TraceContext);
            Assert.Equal(rootDatadogSpan.Context.SpanId, childDatadogSpan.Context.ParentId);
            Assert.Equal(baggageValue, child.Span.GetBaggageItem(baggageKey));
        }

        [Fact]
        public void BuildSpan_2ChildrenOfRoot_ChildrenParentProperlySet()
        {
            const string baggageKey = "BaggageKey";
            const string baggageValue = "BaggageValue";

            IScope root = _tracer
                         .BuildSpan("Root")
                         .StartActive(finishSpanOnDispose: true);

            root.Span.SetBaggageItem(baggageKey, baggageValue);

            IScope child1 = _tracer
                           .BuildSpan("Child1")
                           .StartActive(finishSpanOnDispose: true);

            child1.Dispose();

            IScope child2 = _tracer
                           .BuildSpan("Child2")
                           .StartActive(finishSpanOnDispose: true);

            Span rootDatadogSpan = ((OpenTracingSpan)root.Span).Span;
            Span child1DatadogSpan = ((OpenTracingSpan)child1.Span).Span;
            Span child2DatadogSpan = ((OpenTracingSpan)child2.Span).Span;

            Assert.Same(rootDatadogSpan.Context.TraceContext, child1DatadogSpan.Context.TraceContext);
            Assert.Equal(rootDatadogSpan.Context.SpanId, child1DatadogSpan.Context.ParentId);
            Assert.Equal(baggageValue, child1.Span.GetBaggageItem(baggageKey));

            Assert.Same(rootDatadogSpan.Context.TraceContext, child2DatadogSpan.Context.TraceContext);
            Assert.Equal(rootDatadogSpan.Context.SpanId, child2DatadogSpan.Context.ParentId);
            Assert.Equal(baggageValue, child2.Span.GetBaggageItem(baggageKey));
        }

        [Fact]
        public void BuildSpan_2LevelChildren_ChildrenParentProperlySet()
        {
            const string baggageKeyRoot = "BaggageKeyRoot";
            const string baggageValueRoot = "BaggageValueRoot";

            const string baggageKeyChild1 = "BaggageKeyChild1";
            const string baggageValueChild1 = "BaggageValueChild1";

            IScope root = _tracer
                         .BuildSpan("Root")
                         .StartActive(finishSpanOnDispose: true);

            root.Span.SetBaggageItem(baggageKeyRoot, baggageValueRoot);

            IScope child1 = _tracer
                           .BuildSpan("Child1")
                           .StartActive(finishSpanOnDispose: true);

            child1.Span.SetBaggageItem(baggageKeyChild1, baggageValueChild1);

            IScope child2 = _tracer
                           .BuildSpan("Child2")
                           .StartActive(finishSpanOnDispose: true);

            Span rootDatadogSpan = ((OpenTracingSpan)root.Span).Span;
            Span child1DatadogSpan = ((OpenTracingSpan)child1.Span).Span;
            Span child2DatadogSpan = ((OpenTracingSpan)child2.Span).Span;

            Assert.Same(rootDatadogSpan.Context.TraceContext, child1DatadogSpan.Context.TraceContext);
            Assert.Equal(rootDatadogSpan.Context.SpanId, child1DatadogSpan.Context.ParentId);
            Assert.Equal(baggageValueRoot, child1.Span.GetBaggageItem(baggageKeyRoot));
            Assert.Equal(baggageValueChild1, child1.Span.GetBaggageItem(baggageKeyChild1));

            Assert.Same(rootDatadogSpan.Context.TraceContext, child2DatadogSpan.Context.TraceContext);
            Assert.Equal(child1DatadogSpan.Context.SpanId, child2DatadogSpan.Context.ParentId);
            Assert.Equal(baggageValueRoot, child2.Span.GetBaggageItem(baggageKeyRoot));
            Assert.Equal(baggageValueChild1, child2.Span.GetBaggageItem(baggageKeyChild1));
        }

        [Fact]
        public async Task BuildSpan_AsyncChildrenCreation_ChildrenParentProperlySet()
        {
            var tcs = new TaskCompletionSource<bool>();

            const string baggageKey = "BaggageKey";
            const string baggageValue = "BaggageValue";

            IScope root = _tracer
                         .BuildSpan("Root")
                         .StartActive(finishSpanOnDispose: true);

            root.Span.SetBaggageItem(baggageKey, baggageValue);

            Func<OpenTracingTracer, Task<OpenTracingSpan>> createSpanAsync = async (t) =>
            {
                await tcs.Task;
                return (OpenTracingSpan)_tracer.BuildSpan("AsyncChild").Start();
            };
            var tasks = Enumerable.Range(0, 10).Select(x => createSpanAsync(_tracer)).ToArray();

            var syncChild = (OpenTracingSpan)_tracer.BuildSpan("SyncChild").Start();
            tcs.SetResult(true);

            Span rootDatadogSpan = ((OpenTracingSpan)root.Span).Span;

            Assert.Equal(rootDatadogSpan.Context.TraceContext, (ITraceContext)syncChild.DDSpan.Context.TraceContext);
            Assert.Equal(rootDatadogSpan.Context.SpanId, syncChild.DDSpan.Context.ParentId);

            foreach (var task in tasks)
            {
                var span = await task;
                Assert.Equal(rootDatadogSpan.Context.TraceContext, (ITraceContext)span.DDSpan.Context.TraceContext);
                Assert.Equal(rootDatadogSpan.Context.SpanId, span.DDSpan.Context.ParentId);
                Assert.Equal(baggageValue, span.GetBaggageItem(baggageKey));
            }
        }

        [Fact]
        public void Inject_HttpHeadersFormat_CorrectHeaders()
        {
            const string baggageKey = "BaggageKey";
            const string baggageValue = "BaggageValue";

            var span = (OpenTracingSpan)_tracer.BuildSpan("Span").Start();
            span.SetBaggageItem(baggageKey, baggageValue);
            var headers = new MockTextMap();

            _tracer.Inject(span.Context, BuiltinFormats.HttpHeaders, headers);

            Assert.Equal(span.DDSpan.Context.TraceId.ToString(), headers.Get(HttpHeaderNames.TraceId));
            Assert.Equal(span.DDSpan.Context.SpanId.ToString(), headers.Get(HttpHeaderNames.ParentId));
            Assert.Equal(span.GetBaggageItem(baggageKey), headers.Get($"{OpenTracingHttpHeaderNames.BaggagePrefix}{baggageKey}"));
        }

        [Fact]
        public void Extract_HeadersProperlySet_SpanContext()
        {
            const ulong parentId = 10;
            const ulong traceId = 42;
            const string baggageKey = "BaggageKey";
            const string baggageValue = "BaggageValue";

            var headers = new MockTextMap();
            headers.Set(HttpHeaderNames.ParentId, parentId.ToString());
            headers.Set(HttpHeaderNames.TraceId, traceId.ToString());
            headers.Set($"{OpenTracingHttpHeaderNames.BaggagePrefix}{baggageKey}", baggageValue);

            var otSpanContext = (OpenTracingSpanContext)_tracer.Extract(BuiltinFormats.HttpHeaders, headers);

            Assert.Equal(parentId, otSpanContext.Context.SpanId);
            Assert.Equal(traceId, otSpanContext.Context.TraceId);
            Assert.Equal(baggageValue, otSpanContext.GetBaggageItem(baggageKey));
        }

        [Fact]
        public void StartActive_NoServiceName_DefaultServiceName()
        {
            var scope = _tracer.BuildSpan("Operation")
                               .StartActive();

            var otSpan = (OpenTracingSpan)scope.Span;
            var ddSpan = otSpan.Span;

            Assert.Contains(ddSpan.ServiceName, TestRunners.ValidNames);
        }

        [Fact]
        public void SetDefaultServiceName()
        {
            ITracer tracer = OpenTracingTracerFactory.CreateTracer(defaultServiceName: "DefaultServiceName");

            var scope = tracer.BuildSpan("Operation")
                              .StartActive();

            var otSpan = (OpenTracingSpan)scope.Span;
            var ddSpan = otSpan.Span;

            Assert.Equal("DefaultServiceName", ddSpan.ServiceName);
        }

        [Fact]
        public void SetServiceName_WithTag()
        {
            var scope = _tracer.BuildSpan("Operation")
                               .WithTag(DatadogTags.ServiceName, "MyAwesomeService")
                               .StartActive();

            var otSpan = (OpenTracingSpan)scope.Span;
            var ddSpan = otSpan.Span;

            Assert.Equal("MyAwesomeService", ddSpan.ServiceName);
        }

        [Fact]
        public void SetServiceName_SetTag()
        {
            var scope = _tracer.BuildSpan("Operation")
                               .StartActive();

            scope.Span.SetTag(DatadogTags.ServiceName, "MyAwesomeService");
            var otSpan = (OpenTracingSpan)scope.Span;
            var ddSpan = otSpan.Span;

            Assert.Equal("MyAwesomeService", ddSpan.ServiceName);
        }

        [Fact]
        public void OverrideDefaultServiceName_WithTag()
        {
            ITracer tracer = OpenTracingTracerFactory.CreateTracer(defaultServiceName: "DefaultServiceName");

            var scope = tracer.BuildSpan("Operation")
                              .WithTag(DatadogTags.ServiceName, "MyAwesomeService")
                              .StartActive();

            var otSpan = (OpenTracingSpan)scope.Span;
            var ddSpan = otSpan.Span;

            Assert.Equal("MyAwesomeService", ddSpan.ServiceName);
        }

        [Fact]
        public void OverrideDefaultServiceName_SetTag()
        {
            ITracer tracer = OpenTracingTracerFactory.CreateTracer(defaultServiceName: "DefaultServiceName");

            var scope = tracer.BuildSpan("Operation")
                              .StartActive();

            scope.Span.SetTag(DatadogTags.ServiceName, "MyAwesomeService");
            var otSpan = (OpenTracingSpan)scope.Span;
            var ddSpan = otSpan.Span;

            Assert.Equal("MyAwesomeService", ddSpan.ServiceName);
        }

        [Fact]
        public void InheritParentServiceName_WithTag()
        {
            var parentScope = _tracer.BuildSpan("ParentOperation")
                                     .WithTag(DatadogTags.ServiceName, "MyAwesomeService")
                                     .StartActive();

            var childScope = _tracer.BuildSpan("ChildOperation")
                                    .AsChildOf(parentScope.Span)
                                    .StartActive();

            var otSpan = (OpenTracingSpan)childScope.Span;
            var ddSpan = otSpan.Span;

            Assert.Equal("MyAwesomeService", ddSpan.ServiceName);
        }

        [Fact]
        public void InheritParentServiceName_SetTag()
        {
            var parentScope = _tracer.BuildSpan("ParentOperation")
                                     .StartActive();

            parentScope.Span.SetTag(DatadogTags.ServiceName, "MyAwesomeService");

            var childScope = _tracer.BuildSpan("ChildOperation")
                                    .AsChildOf(parentScope.Span)
                                    .StartActive();

            var otSpan = (OpenTracingSpan)childScope.Span;
            var ddSpan = otSpan.Span;

            Assert.Equal("MyAwesomeService", ddSpan.ServiceName);
        }

        [Fact]
        public void Parent_OverrideDefaultServiceName_WithTag()
        {
            ITracer tracer = OpenTracingTracerFactory.CreateTracer(defaultServiceName: "DefaultServiceName");

            var parentScope = tracer.BuildSpan("ParentOperation")
                                    .WithTag(DatadogTags.ServiceName, "MyAwesomeService")
                                    .StartActive();

            var childScope = tracer.BuildSpan("ChildOperation")
                                   .AsChildOf(parentScope.Span)
                                   .StartActive();

            var otSpan = (OpenTracingSpan)childScope.Span;
            var ddSpan = otSpan.Span;

            Assert.Equal("MyAwesomeService", ddSpan.ServiceName);
        }

        [Fact]
        public void Parent_OverrideDefaultServiceName_SetTag()
        {
            ITracer tracer = OpenTracingTracerFactory.CreateTracer(defaultServiceName: "DefaultServiceName");

            var parentScope = tracer.BuildSpan("ParentOperation")
                                    .StartActive();

            parentScope.Span.SetTag(DatadogTags.ServiceName, "MyAwesomeService");

            var childScope = tracer.BuildSpan("ChildOperation")
                                   .AsChildOf(parentScope.Span)
                                   .StartActive();

            var otSpan = (OpenTracingSpan)childScope.Span;
            var ddSpan = otSpan.Span;

            Assert.Equal("MyAwesomeService", ddSpan.ServiceName);
        }
    }
}
