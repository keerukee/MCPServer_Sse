# McpSseServer

A lightweight .NET library for building **MCP (Model Context Protocol)** servers with **SSE (Server-Sent Events)** transport in ASP.NET Core.

[![NuGet](https://img.shields.io/nuget/v/McpSseServer.svg)](https://www.nuget.org/packages/McpSseServer)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

## ?? Features

- **Minimal Setup** - Get an MCP server running in just 8 lines of code
- **Auto-Discovery** - Classes marked with `[McpHandler]` are automatically registered
- **Strongly-Typed** - Write normal C# methods with typed parameters - no JSON parsing!
- **SSE Transport** - Full Server-Sent Events support with HTTP/1.1 compatibility
- **Fallback Support** - Automatic fallback to direct HTTP responses

## ?? Installation

```bash
dotnet add package McpSseServer
```

Or via Package Manager:
```powershell
Install-Package McpSseServer
```

## ?? Quick Start

### Step 1: Create a new ASP.NET Core Web API project

```bash
dotnet new web -n MyMcpServer
cd MyMcpServer
dotnet add package McpSseServer
```

### Step 2: Configure Program.cs

```csharp
using McpSseServer;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureKestrelForSse();
builder.Services.AddMcpSseServer(options =>
{
    options.ServerName = "My-MCP-Server";
    options.ServerVersion = "1.0.0";
});

var app = builder.Build();

app.UseMcpSseServer();  // Auto-discovers all [McpHandler] classes!

app.Run();
```

### Step 3: Create your handlers

Create a new file `Handlers/MyTools.cs`:

```csharp
using McpSseServer.Attributes;

namespace MyMcpServer.Handlers;

[McpHandler]
public class MyTools
{
    [McpTool("greet", "Greets a person by name")]
    public string Greet(string name)
    {
        return $"Hello, {name}!";
    }

    [McpTool("add", "Adds two numbers")]
    public string Add(double a, double b)
    {
        return $"Result: {a + b}";
    }
}
```

### Step 4: Run and connect

```bash
dotnet run
```

Your MCP server is now running at `http://localhost:5000/sse` ??

---

## ?? Creating Tools

Tools are functions that the AI can call to perform actions.

### Basic Tool

```csharp
using McpSseServer.Attributes;

[McpHandler]
public class BasicTools
{
    [McpTool("generate_uuid", "Generates a new random UUID")]
    public string GenerateUuid()
    {
        return Guid.NewGuid().ToString();
    }
}
```

### Tool with Parameters

```csharp
[McpHandler]
public class MathTools
{
    [McpTool("calculate", "Performs arithmetic calculations")]
    public string Calculate(
        [McpParameter("Operation to perform", EnumValues = ["add", "subtract", "multiply", "divide"])] string operation,
        [McpParameter("First operand")] double a,
        [McpParameter("Second operand")] double b)
    {
        var result = operation switch
        {
            "add" => a + b,
            "subtract" => a - b,
            "multiply" => a * b,
            "divide" when b != 0 => a / b,
            "divide" => double.NaN,
            _ => throw new ArgumentException($"Unknown operation: {operation}")
        };

        return double.IsNaN(result) ? "Error: Division by zero" : $"{a} {operation} {b} = {result}";
    }
}
```

### Tool with Optional Parameters

```csharp
[McpHandler]
public class SearchTools
{
    [McpTool("search", "Searches for items")]
    public string Search(
        [McpParameter("Search query")] string query,
        [McpParameter("Maximum results")] int limit = 10,      // Optional with default
        [McpParameter("Sort order")] string? order = null)     // Nullable = optional
    {
        return $"Searching '{query}' with limit {limit}, order {order ?? "default"}";
    }
}
```

### Parameter Type Mapping

| C# Type | JSON Schema Type | Required |
|---------|------------------|----------|
| `string` | `string` | Yes |
| `int`, `long` | `integer` | Yes |
| `double`, `float` | `number` | Yes |
| `bool` | `boolean` | Yes |
| `enum` | `string` (with values) | Yes |
| `string?`, `int?` | Same as base | No |
| `param = value` | Same as base | No |

---

## ?? Creating Resources

Resources provide read-only data that the AI can access.

```csharp
using System.Text.Json;
using McpSseServer.Attributes;

[McpHandler]
public class MyResources
{
    [McpResource("config://app/settings", "App Settings", "Application configuration", "application/json")]
    public string GetAppSettings()
    {
        return JsonSerializer.Serialize(new
        {
            theme = "dark",
            language = "en",
            version = "1.0.0"
        });
    }

    [McpResource("docs://readme", "README", "Application documentation", "text/plain")]
    public string GetReadme()
    {
        return "# My Application\n\nWelcome to the documentation!";
    }

    [McpResource("info://server/status", "Server Status", "Current server status", "application/json")]
    public string GetServerStatus()
    {
        return JsonSerializer.Serialize(new
        {
            status = "running",
            timestamp = DateTime.UtcNow,
            uptime = TimeSpan.FromMinutes(42)
        });
    }
}
```

### Resource Attribute Parameters

```csharp
[McpResource(
    uri: "scheme://path/to/resource",  // Unique identifier
    name: "Display Name",               // Human-readable name
    description: "Description",         // What the resource contains
    mimeType: "application/json"        // Content type (default: text/plain)
)]
```

---

## ?? Creating Prompts

Prompts are reusable templates that help guide AI interactions.

```csharp
using McpSseServer.Attributes;

[McpHandler]
public class MyPrompts
{
    [McpPrompt("greeting", "Generates a personalized greeting")]
    public string Greeting(
        [McpArgument("Name to greet")] string name,
        [McpArgument("Style: formal, casual, enthusiastic")] string style = "casual")
    {
        return style switch
        {
            "formal" => $"Dear {name}, I hope this message finds you well.",
            "enthusiastic" => $"Hey {name}! SO excited to chat with you!",
            _ => $"Hi {name}! How can I help you today?"
        };
    }

    [McpPrompt("code_review", "Creates a code review prompt")]
    public string CodeReview(
        [McpArgument("Programming language")] string language,
        [McpArgument("Focus area")] string focus = "general")
    {
        return $"Please review the following {language} code with focus on {focus}.";
    }

    [McpPrompt("summarize", "Creates a summarization prompt")]
    public string Summarize(
        [McpArgument("Summary length: brief, medium, detailed")] string length = "medium")
    {
        return length switch
        {
            "brief" => "Provide a 1-2 sentence summary.",
            "detailed" => "Provide a comprehensive summary with all key points.",
            _ => "Provide a balanced summary in 3-5 sentences."
        };
    }
}
```

---

## ?? Configuration Options

```csharp
builder.Services.AddMcpSseServer(options =>
{
    // Server identity
    options.ServerName = "My-MCP-Server";
    options.ServerVersion = "1.0.0";
    
    // MCP protocol version
    options.ProtocolVersion = "2024-11-05";
    
    // Logging
    options.EnableLogging = true;      // Log MCP requests/responses
    options.EnableHttpLogging = true;  // Log HTTP requests
    
    // Endpoints (customize if needed)
    options.SseEndpoint = "/sse";           // SSE connection endpoint
    options.MessageEndpoint = "/message";   // Message endpoint
});
```

---

## ?? Attribute Reference

| Attribute | Target | Purpose |
|-----------|--------|---------|
| `[McpHandler]` | Class | Marks class for auto-discovery |
| `[McpTool(name, description)]` | Method | Defines a callable tool |
| `[McpResource(uri, name, desc, mime)]` | Method | Defines a readable resource |
| `[McpPrompt(name, description)]` | Method | Defines a prompt template |
| `[McpParameter(description)]` | Parameter | Adds metadata to tool parameters |
| `[McpArgument(description)]` | Parameter | Adds metadata to prompt arguments |

---

## ?? Connecting Clients

### MCP Inspector

```bash
npx @anthropic/mcp-inspector http://localhost:5000/sse
```

### Claude Desktop

Add to your Claude Desktop config (`claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "my-server": {
      "url": "http://localhost:5000/sse"
    }
  }
}
```

### Custom Client

Connect to the SSE endpoint at `/sse`. The server will send an `endpoint` event with the message URL:

```
event: endpoint
data: http://localhost:5000/message?sessionId=<guid>
```

---

## ?? Example Project Structure

```
MyMcpServer/
??? Program.cs
??? Handlers/
?   ??? ToolHandlers.cs      # [McpHandler] with [McpTool] methods
?   ??? ResourceHandlers.cs  # [McpHandler] with [McpResource] methods
?   ??? PromptHandlers.cs    # [McpHandler] with [McpPrompt] methods
??? Properties/
?   ??? launchSettings.json
??? MyMcpServer.csproj
```

---

## ?? Complete Example

### Program.cs

```csharp
using McpSseServer;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureKestrelForSse();
builder.Services.AddMcpSseServer(options =>
{
    options.ServerName = "Demo-MCP-Server";
    options.ServerVersion = "1.0.0";
});

var app = builder.Build();
app.UseMcpSseServer();
app.Run();
```

### Handlers/DemoHandlers.cs

```csharp
using System.Text.Json;
using McpSseServer.Attributes;

namespace MyMcpServer.Handlers;

[McpHandler]
public class DemoHandlers
{
    // === TOOLS ===
    
    [McpTool("get_time", "Gets the current time")]
    public string GetTime(string timezone = "UTC")
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
        var time = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        return $"Current time in {timezone}: {time:yyyy-MM-dd HH:mm:ss}";
    }

    [McpTool("calculate", "Basic calculator")]
    public string Calculate(string operation, double a, double b)
    {
        var result = operation switch
        {
            "add" => a + b,
            "subtract" => a - b,
            "multiply" => a * b,
            "divide" => a / b,
            _ => throw new ArgumentException($"Unknown: {operation}")
        };
        return $"{a} {operation} {b} = {result}";
    }

    // === RESOURCES ===
    
    [McpResource("info://status", "Status", "Server status", "application/json")]
    public string GetStatus()
    {
        return JsonSerializer.Serialize(new { status = "ok", time = DateTime.UtcNow });
    }

    // === PROMPTS ===
    
    [McpPrompt("hello", "Greeting prompt")]
    public string Hello(string name) => $"Hello {name}! How can I help?";
}
```

---

## ?? Source Code

GitHub Repository: **[https://github.com/keerukee/MCPServer_Sse.git](https://github.com/keerukee/MCPServer_Sse.git)**

---

## ?? License

MIT License - feel free to use in your projects!

---

## ?? Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
