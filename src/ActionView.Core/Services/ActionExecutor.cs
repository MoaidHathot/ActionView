using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ActionView.Core.Models;
using Microsoft.Extensions.Logging;

namespace ActionView.Core.Services;

/// <summary>
/// Executes action commands (HTTP requests or CLI processes)
/// defined in entry actions.
/// </summary>
public sealed class ActionExecutor
{
    private readonly SecretResolver _secretResolver;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ActionExecutor> _logger;

    public ActionExecutor(SecretResolver secretResolver, HttpClient httpClient, ILogger<ActionExecutor> logger)
    {
        _secretResolver = secretResolver;
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Executes an action command and returns the result.
    /// </summary>
    public async Task<ActionExecutionResult> ExecuteAsync(ActionCommand command, CancellationToken ct = default)
    {
        return command.Type switch
        {
            CommandType.Http => await ExecuteHttpAsync(command, ct),
            CommandType.Cli => await ExecuteCliAsync(command, ct),
            _ => new ActionExecutionResult { Success = false, Message = $"Unknown command type: {command.Type}" }
        };
    }

    private async Task<ActionExecutionResult> ExecuteHttpAsync(ActionCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Url))
            return new ActionExecutionResult { Success = false, Message = "HTTP command missing URL" };

        var method = command.Method?.ToUpperInvariant() switch
        {
            "GET" => HttpMethod.Get,
            "POST" => HttpMethod.Post,
            "PUT" => HttpMethod.Put,
            "PATCH" => HttpMethod.Patch,
            "DELETE" => HttpMethod.Delete,
            _ => HttpMethod.Post
        };

        var url = _secretResolver.Resolve(command.Url);
        var request = new HttpRequestMessage(method, url);

        // Add headers with secret resolution
        if (command.Headers is not null)
        {
            foreach (var (key, value) in command.Headers)
            {
                var resolvedValue = _secretResolver.Resolve(value);

                // Handle Authorization header specially
                if (key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = resolvedValue.Split(' ', 2);
                    if (parts.Length == 2)
                        request.Headers.Authorization = new AuthenticationHeaderValue(parts[0], parts[1]);
                    else
                        request.Headers.TryAddWithoutValidation(key, resolvedValue);
                }
                else
                {
                    request.Headers.TryAddWithoutValidation(key, resolvedValue);
                }
            }
        }

        // Add body with secret resolution
        if (command.Body is not null)
        {
            var bodyJson = command.Body.Value.GetRawText();
            var resolvedBody = _secretResolver.Resolve(bodyJson);
            request.Content = new StringContent(resolvedBody, Encoding.UTF8, "application/json");
        }

        try
        {
            _logger.LogInformation("Executing HTTP {Method} {Url}", method, url);
            var response = await _httpClient.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            return new ActionExecutionResult
            {
                Success = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                Message = response.IsSuccessStatusCode
                    ? "Request completed successfully"
                    : $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                Output = responseBody.Length > 2000 ? responseBody[..2000] + "..." : responseBody
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP request failed: {Url}", url);
            return new ActionExecutionResult
            {
                Success = false,
                Message = $"HTTP request failed: {ex.Message}"
            };
        }
    }

    private async Task<ActionExecutionResult> ExecuteCliAsync(ActionCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Program))
            return new ActionExecutionResult { Success = false, Message = "CLI command missing program" };

        var program = _secretResolver.Resolve(command.Program);
        var args = command.Args?.Select(a => _secretResolver.Resolve(a)).ToList() ?? [];

        var psi = new ProcessStartInfo
        {
            FileName = program,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        if (!string.IsNullOrWhiteSpace(command.WorkingDirectory))
            psi.WorkingDirectory = _secretResolver.Resolve(command.WorkingDirectory);

        try
        {
            _logger.LogInformation("Executing CLI: {Program} {Args}", program, string.Join(" ", args));
            using var process = Process.Start(psi);

            if (process is null)
                return new ActionExecutionResult { Success = false, Message = "Failed to start process" };

            var stdout = await process.StandardOutput.ReadToEndAsync(ct);
            var stderr = await process.StandardError.ReadToEndAsync(ct);

            await process.WaitForExitAsync(ct);

            var output = string.IsNullOrWhiteSpace(stderr) ? stdout : $"{stdout}\n--- stderr ---\n{stderr}";

            return new ActionExecutionResult
            {
                Success = process.ExitCode == 0,
                StatusCode = process.ExitCode,
                Message = process.ExitCode == 0 ? "Process completed successfully" : $"Process exited with code {process.ExitCode}",
                Output = output.Length > 2000 ? output[..2000] + "..." : output
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CLI execution failed: {Program}", program);
            return new ActionExecutionResult
            {
                Success = false,
                Message = $"CLI execution failed: {ex.Message}"
            };
        }
    }
}
