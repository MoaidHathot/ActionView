using System.Text.Json;

namespace ActionView.Core.Models;

/// <summary>
/// Describes a command to execute when an action is triggered.
/// Supports HTTP requests and CLI process execution.
/// Secret values can be referenced as {{VAR_NAME}} and are resolved
/// from environment variables at execution time.
/// </summary>
public sealed class ActionCommand
{
    /// <summary>The type of command: http or cli.</summary>
    public required CommandType Type { get; set; }

    // --- HTTP specific ---

    /// <summary>HTTP method (GET, POST, PUT, PATCH, DELETE).</summary>
    public string? Method { get; set; }

    /// <summary>Target URL for the HTTP request.</summary>
    public string? Url { get; set; }

    /// <summary>HTTP headers. Values may contain {{VAR}} placeholders.</summary>
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>HTTP request body. Values within may contain {{VAR}} placeholders.</summary>
    public JsonElement? Body { get; set; }

    // --- CLI specific ---

    /// <summary>Program/executable to run.</summary>
    public string? Program { get; set; }

    /// <summary>Arguments to pass to the program.</summary>
    public List<string>? Args { get; set; }

    /// <summary>Working directory for the CLI process.</summary>
    public string? WorkingDirectory { get; set; }
}

public enum CommandType
{
    Http,
    Cli
}
