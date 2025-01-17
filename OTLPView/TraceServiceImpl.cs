using Google.Protobuf.Collections;
using Grpc.Core;
using OpenTelemetry.Proto.Collector.Trace.V1;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using static OTLPView.TraceServiceImpl;
using Otel = OpenTelemetry.Proto.Trace.V1;
using OTLPView.DataModel;
using OpenTelemetry.Proto.Common.V1;

namespace OTLPView
{
    public class TraceServiceImpl : OpenTelemetry.Proto.Collector.Trace.V1.TraceService.TraceServiceBase
    {
        private readonly ILogger<TraceServiceImpl> _logger;
        private readonly TelemetryResults _telemetryResults;
        private readonly TracesPageState _pageState;

        public TraceServiceImpl(ILogger<TraceServiceImpl> logger, TelemetryResults telemetryResults, TracesPageState pageState)
        {
            _logger = logger;
            _telemetryResults = telemetryResults;
            _pageState = pageState;
        }

        public override Task<ExportTraceServiceResponse> Export(ExportTraceServiceRequest request, ServerCallContext context)
        {
            ProcessTraceGrpcResourceSpans(request.ResourceSpans);
            _pageState.DataChanged();

            var resp = new ExportTraceServiceResponse();
            resp.PartialSuccess = null;
            return Task.FromResult(resp);
        }


        private void ProcessTraceGrpcResourceSpans(RepeatedField<Otel.ResourceSpans> resourceSpans)
        {
            foreach (var r in resourceSpans)
            {
                // Store the trace source if we haven't seen it before
                var traceSource = _telemetryResults.GetOrAddApplication(r.Resource);

                foreach (var scopeSpan in r.ScopeSpans)
                {
                    var scopeName = scopeSpan.Scope.Name;
                    var traceScope = traceSource.GetOrAddTrace(scopeName, _ =>
                    {
                        var color = $"#{traceSource.ColorSequence[traceSource.Scopes.Count]:X6}";
                        return new TraceScope(scopeSpan.Scope, color);
                    });

                    foreach (var sp in scopeSpan.Spans)
                    {
                        var operationId = sp.TraceId.ToHexString();
                        var operation = _telemetryResults.GetOrAddOperation(operationId);
                        var span = new TraceSpan(sp, operation, traceSource, traceScope);
                    }
                }
            }
        }

    }
}
