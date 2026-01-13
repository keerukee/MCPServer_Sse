namespace McpSseServer;

/// <summary>
/// Configuration options for the MCP SSE Server.
/// </summary>
public class McpServerOptions
{
    /// <summary>
    /// The name of the MCP server (shown to clients).
    /// </summary>
    public string ServerName { get; set; } = "MCP-SSE-Server";

    /// <summary>
    /// The version of the MCP server.
    /// </summary>
    public string ServerVersion { get; set; } = "1.0.0";

    /// <summary>
    /// The MCP protocol version to advertise.
    /// </summary>
    public string ProtocolVersion { get; set; } = "2024-11-05";

    /// <summary>
    /// Enable console logging of MCP requests/responses.
    /// </summary>
    public bool EnableLogging { get; set; } = true;

    /// <summary>
    /// Enable detailed HTTP request logging.
    /// </summary>
    public bool EnableHttpLogging { get; set; } = true;

    /// <summary>
    /// The route path for the SSE endpoint.
    /// </summary>
    public string SseEndpoint { get; set; } = "/sse";

    /// <summary>
    /// The route path for the message endpoint.
    /// </summary>
    public string MessageEndpoint { get; set; } = "/message";
}
