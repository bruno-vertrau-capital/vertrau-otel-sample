using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;
using Serilog;
using Serilog.Enrichers.Span;
using System.Text;


var serverUrl = "http://localhost:12345";
const string appName = "app-test";
const string otlpExporterUri = "http://localhost:4317";

using var bootstrapLoggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Trace);
});

var logger = bootstrapLoggerFactory.CreateLogger<Program>();

try
{

    var builder = WebApplication.CreateBuilder(args);
    builder.Configuration.AddEnvironmentVariables();
    IServiceCollection services = builder.Services;

    CompositeTextMapPropagator compositeTextMapPropagator = new(
    [
        new TraceContextPropagator(),
        new BaggagePropagator(),
    ]);

    OpenTelemetry.Sdk.SetDefaultTextMapPropagator(compositeTextMapPropagator);

    var loggerConfiguration = new LoggerConfiguration()
        .WriteTo.Console(Serilog.Events.LogEventLevel.Information)
        .Enrich.FromLogContext();

    SpanOptions spanOptions = new()
    {
        IncludeOperationName = true,
        IncludeTags = true,
        IncludeBaggage = true,
        IncludeTraceFlags = true
    };

    services.AddSerilog(loggerConfiguration
            .Enrich.WithSpan(spanOptions)
            .CreateLogger());

    services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
    .SetResourceBuilder(
        ResourceBuilder.CreateDefault()
            .AddService(
                serviceName: appName,
                serviceVersion: "1.0.0",
                serviceInstanceId: Environment.MachineName))
    .AddAspNetCoreInstrumentation()
    .AddHttpClientInstrumentation()
    .AddRuntimeInstrumentation()
    .AddPrometheusExporter())
    .WithTracing((traceBuilder) =>
    {
        traceBuilder
            .AddSource(appName)
            .SetResourceBuilder(
                ResourceBuilder.CreateDefault()
                    .AddService(
                        serviceName: appName,
                        serviceVersion: "1.0.0",
                        serviceInstanceId: Environment.MachineName))
            .AddHttpClientInstrumentation(options => options.RecordException = true)
            .AddAspNetCoreInstrumentation(options => options.RecordException = true)
            .AddOtlpExporter(options => options.Endpoint = new Uri(otlpExporterUri));
    })
    .WithLogging((builder) => builder.AddConsoleExporter());


    services.AddHealthChecks()
             .AddCheck("self", () => HealthCheckResult.Healthy(), ["server"])
             .ForwardToPrometheus();

    services.AddHttpLogging(logging =>
    {
        logging.LoggingFields = HttpLoggingFields.All;
        logging.MediaTypeOptions.AddText("application/json", Encoding.UTF8);
        logging.MediaTypeOptions.AddText("application/*+json", Encoding.UTF8);
        logging.MediaTypeOptions.AddText("application/xml", Encoding.UTF8);
        logging.MediaTypeOptions.AddText("application/*+xml", Encoding.UTF8);
        logging.MediaTypeOptions.AddText("text/*", Encoding.UTF8);
    });

    var app = builder.Build();
    app.UseRouting()
       .UseHttpLogging()
       .UseHttpMetrics()
       .UseSerilogRequestLogging(options => options.IncludeQueryInRequestPath = true);

    app.MapPrometheusScrapingEndpoint();
    app.MapHealthChecks("/readiness", new HealthCheckOptions()
    {
        Predicate = r => r.Name.Contains("self"),
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
    }).WithMetadata(new AllowAnonymousAttribute());
    app.MapHealthChecks("/healthcheck", new HealthCheckOptions()
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
    }).WithMetadata(new AllowAnonymousAttribute());

    app.MapGet("/", () => "hello world");    
    await app.RunAsync(serverUrl);
}
catch (Exception ex)
{
    logger.LogError(ex, "Ocorreu um erro não esperado.");
}