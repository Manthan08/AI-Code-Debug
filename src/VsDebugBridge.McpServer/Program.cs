using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VsDebugBridge.Ipc;
using VsDebugBridge.McpServer.Tools;

var builder = Host.CreateApplicationBuilder(args);

// Configure all logs to go to stderr (stdout is used for the MCP protocol messages).
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// Add the MCP services: the transport to use (stdio) and the tools to register.
builder.Services.AddSingleton<DiscoveryStore>();
builder.Services.AddSingleton<NamedPipeBridgeClient>();
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<VisualStudioDebugTools>();

await builder.Build().RunAsync();
