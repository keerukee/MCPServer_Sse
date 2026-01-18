using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using McpSseServer.Attributes;

namespace McpSseServer;

/// <summary>
/// Extension methods for configuring MCP SSE Server in ASP.NET Core applications.
/// </summary>
public static class McpServerExtensions
{
    /// <summary>
    /// Adds MCP SSE Server services to the service collection.
    /// </summary>
    public static IServiceCollection AddMcpSseServer(
        this IServiceCollection services,
        Action<McpServerOptions>? configureOptions = null)
    {
        var options = new McpServerOptions();
        configureOptions?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<McpRegistry>();
        services.AddSingleton<McpRequestProcessor>();
        services.AddSingleton<ConcurrentDictionary<string, SseSession>>();

        if (options.UseDefaultCors)
        {
            services.AddCors(corsOptions =>
            {
                corsOptions.AddPolicy("McpCors", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .WithExposedHeaders("*");
                });
            });
        }

        return services;
    }

    /// <summary>
    /// Configures Kestrel for SSE compatibility (HTTP/1.1).
    /// </summary>
    public static WebApplicationBuilder ConfigureKestrelForSse(this WebApplicationBuilder builder)
    {
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ConfigureEndpointDefaults(listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http1;
            });
        });

        return builder;
    }

    /// <summary>
    /// Maps MCP SSE Server endpoints and auto-discovers handlers from the entry assembly.
    /// Classes marked with [McpHandler] attribute will be automatically registered.
    /// </summary>
    public static WebApplication UseMcpSseServer(this WebApplication app)
    {
        var entryAssembly = Assembly.GetEntryAssembly();
        var callingAssembly = Assembly.GetCallingAssembly();
        
        var assemblies = new HashSet<Assembly>();
        if (entryAssembly != null) assemblies.Add(entryAssembly);
        assemblies.Add(callingAssembly);

        return app.UseMcpSseServerCore(assemblies.ToArray());
    }

    /// <summary>
    /// Maps MCP SSE Server endpoints and registers handlers from the specified types.
    /// </summary>
    public static WebApplication UseMcpSseServer(this WebApplication app, params Type[] handlerTypes)
    {
        var registry = app.Services.GetRequiredService<McpRegistry>();
        
        if (handlerTypes.Length > 0)
            registry.RegisterFromTypes(handlerTypes);

        return app.UseMcpSseServerCore();
    }

    /// <summary>
    /// Maps MCP SSE Server endpoints and auto-discovers handlers from specified assemblies.
    /// </summary>
    public static WebApplication UseMcpSseServer(this WebApplication app, params Assembly[] assemblies)
    {
        return app.UseMcpSseServerCore(assemblies);
    }

    private static WebApplication UseMcpSseServerCore(this WebApplication app, Assembly[]? assemblies = null)
    {
        var options = app.Services.GetRequiredService<McpServerOptions>();
        var registry = app.Services.GetRequiredService<McpRegistry>();
        var processor = app.Services.GetRequiredService<McpRequestProcessor>();
        var sessions = app.Services.GetRequiredService<ConcurrentDictionary<string, SseSession>>();

        // Auto-discover handlers from assemblies
        if (assemblies != null && assemblies.Length > 0)
        {
            foreach (var assembly in assemblies)
            {
                var handlerTypes = assembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && t.GetCustomAttribute<McpHandlerAttribute>() != null)
                    .ToArray();

                if (handlerTypes.Length > 0)
                {
                    registry.RegisterFromTypes(handlerTypes);
                }
            }
        }

        if (options.UseDefaultCors)
        {
            app.UseCors("McpCors");
        }

        if (options.EnableHttpLogging)
        {
            app.Use(async (context, next) =>
            {
                Console.WriteLine($"[HTTP] {context.Request.Method} {context.Request.Path}{context.Request.QueryString}");
                await next();
            });
        }

        // SSE Endpoint - GET
        app.MapGet(options.SseEndpoint, async (HttpContext ctx) =>
        {
            ctx.Response.Headers.ContentType = "text/event-stream";
            ctx.Response.Headers.CacheControl = "no-cache";
            ctx.Response.Headers["X-Accel-Buffering"] = "no";

            if (ctx.Request.Protocol == "HTTP/1.1")
                ctx.Response.Headers.Connection = "keep-alive";

            string sessionId = Guid.NewGuid().ToString();
            var session = new SseSession(ctx.Response);
            sessions[sessionId] = session;

            if (options.EnableLogging)
                Console.WriteLine($"[MCP] SSE Connected: {sessionId}");

            var request = ctx.Request;
            var baseUrl = $"{request.Scheme}://{request.Host}";
            string endpointUri = $"{baseUrl}{options.MessageEndpoint}?sessionId={sessionId}";

            await session.SendEventAsync("endpoint", endpointUri);

            var tcs = new TaskCompletionSource();
            using var registration = ctx.RequestAborted.Register(() => tcs.TrySetResult());

            try
            {
                await tcs.Task;
            }
            finally
            {
                sessions.TryRemove(sessionId, out _);
                session.Dispose();
                if (options.EnableLogging)
                    Console.WriteLine($"[MCP] SSE Disconnected: {sessionId}");
            }
        });

        // SSE Endpoint - POST
        app.MapPost(options.SseEndpoint, async (HttpContext ctx) =>
        {
            using var reader = new StreamReader(ctx.Request.Body);
            var requestJson = await reader.ReadToEndAsync();
            var responseJson = processor.Process(requestJson);
            return responseJson == null ? Results.Accepted() : Results.Content(responseJson, "application/json");
        });

        // SSE Endpoint - DELETE
        app.MapDelete(options.SseEndpoint, () => Results.Ok());

        // Message Endpoint
        app.MapPost(options.MessageEndpoint, async (HttpContext ctx) =>
        {
            var sessionId = ctx.Request.Query["sessionId"].FirstOrDefault();
            using var reader = new StreamReader(ctx.Request.Body);
            var requestJson = await reader.ReadToEndAsync();
            var responseJson = processor.Process(requestJson);

            if (responseJson == null)
                return Results.Accepted();

            SseSession? session = null;
            bool hasSession = !string.IsNullOrEmpty(sessionId) && sessions.TryGetValue(sessionId, out session);

            if (hasSession && session != null)
            {
                try
                {
                    await session.SendEventAsync("message", responseJson);
                    return Results.Accepted();
                }
                catch
                {
                    // Fall through to direct response
                }
            }

            return Results.Content(responseJson, "application/json");
        });

        // Print startup info
        Console.WriteLine("\n========================================");
        Console.WriteLine($"{options.ServerName} v{options.ServerVersion}");
        Console.WriteLine("========================================");
        Console.WriteLine($"SSE Endpoint: {options.SseEndpoint}");
        Console.WriteLine($"Message Endpoint: {options.MessageEndpoint}");
        Console.WriteLine($"Tools: {registry.Tools.Count}");
        Console.WriteLine($"Resources: {registry.Resources.Count}");
        Console.WriteLine($"Prompts: {registry.Prompts.Count}");
        Console.WriteLine("========================================\n");

        return app;
    }
}
