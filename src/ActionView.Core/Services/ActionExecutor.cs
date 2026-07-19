using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using ActionView.Core.Models;
using Microsoft.Extensions.Logging;

namespace ActionView.Core.Services;

/// <summary>
/// Executes action commands (HTTP requests or CLI processes) defined in entry actions.
/// Substitutes placeholders in this order before running:
/// <c>{{param.NAME}}</c> (runtime input) → <c>{{content.*}}</c>/<c>{{entry.*}}</c>
/// (entry data, possibly edited) → <c>{{SECRET}}</c> (config/env).
///
/// Two execution shapes are provided: a buffered <see cref="ExecuteAsync"/> (used
/// by batch/undo) and a streaming <see cref="ExecuteStreamingAsync"/> (used by
/// <see cref="ActionJobRunner"/>) that reports CLI output line-by-line and
/// supports cancellation (killing the process tree).
/// </summary>
public sealed class ActionExecutor
{
    private readonly ParameterResolver _parameterResolver;
    private readonly ContentReferenceResolver _contentResolver;
    private readonly SecretResolver _secretResolver;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ActionExecutor> _logger;

    public ActionExecutor(
        ParameterResolver parameterResolver,
        ContentReferenceResolver contentResolver,
        SecretResolver secretResolver,
        HttpClient httpClient,
        ILogger<ActionExecutor> logger)
    {
        _parameterResolver = parameterResolver;
        _contentResolver = contentResolver;
        _secretResolver = secretResolver;
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>Executes a command and returns the buffered result (no streaming).</summary>
    public async Task<ActionExecutionResult> ExecuteAsync(
        ActionCommand command,
        IReadOnlyDictionary<string, string>? parameters = null,
        ActionContext? context = null,
        CancellationToken ct = default)
    {
        return command.Type switch
        {
            CommandType.Http => await ExecuteHttpAsync(command, parameters, context, ct),
            CommandType.Cli => await ExecuteCliAsync(command, parameters, context, onOutput: null, ct),
            _ => new ActionExecutionResult { Success = false, Message = $"Unknown command type: {command.Type}" }
        };
    }

    /// <summary>
    /// Executes a command, invoking <paramref name="onOutput"/> for each CLI output
    /// line as it arrives. HTTP has no streaming (it reports running → done).
    /// Cancellation kills the process tree and surfaces as
    /// <see cref="OperationCanceledException"/> so the caller can mark it cancelled.
    /// </summary>
    public async Task<ActionExecutionResult> ExecuteStreamingAsync(
        ActionCommand command,
        IReadOnlyDictionary<string, string>? parameters,
        ActionContext? context,
        Action<string>? onOutput,
        CancellationToken ct = default)
    {
        return command.Type switch
        {
            CommandType.Http => await ExecuteHttpAsync(command, parameters, context, ct),
            CommandType.Cli => await ExecuteCliAsync(command, parameters, context, onOutput, ct),
            _ => new ActionExecutionResult { Success = false, Message = $"Unknown command type: {command.Type}" }
        };
    }

    private async Task<ActionExecutionResult> ExecuteHttpAsync(
        ActionCommand command,
        IReadOnlyDictionary<string, string>? parameters,
        ActionContext? context,
        CancellationToken ct)
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

        var url = ResolveAll(command.Url, parameters, context);
        var request = new HttpRequestMessage(method, url);

        // Add headers with parameter + content + secret resolution
        if (command.Headers is not null)
        {
            foreach (var (key, value) in command.Headers)
            {
                var resolvedValue = ResolveAll(value, parameters, context);

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

        // Body: substitute parameters + content refs at the JSON-leaf level (preserves
        // structure and quoting) and then resolve secrets in the resulting raw JSON.
        if (command.Body is not null)
        {
            var bodyJson = JsonElementParameterizer.Parameterize(
                command.Body.Value,
                leaf => _contentResolver.Resolve(_parameterResolver.Resolve(leaf, parameters), context));
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
        catch (OperationCanceledException)
        {
            throw;
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

    private async Task<ActionExecutionResult> ExecuteCliAsync(
        ActionCommand command,
        IReadOnlyDictionary<string, string>? parameters,
        ActionContext? context,
        Action<string>? onOutput,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Program))
            return new ActionExecutionResult { Success = false, Message = "CLI command missing program" };

        var program = ResolveAll(command.Program, parameters, context);
        var args = command.Args?.Select(a => ResolveAll(a, parameters, context)).ToList() ?? [];

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
            psi.WorkingDirectory = ResolveAll(command.WorkingDirectory, parameters, context);

        var collected = new List<string>();
        var collectLock = new object();

        void Sink(string line)
        {
            lock (collectLock) collected.Add(line);
            onOutput?.Invoke(line);
        }

        try
        {
            _logger.LogInformation("Executing CLI: {Program} {Args}", program, string.Join(" ", args));
            using var process = new Process { StartInfo = psi };

            if (!process.Start())
                return new ActionExecutionResult { Success = false, Message = "Failed to start process" };

            var pumpOut = PumpAsync(process.StandardOutput, Sink, ct);
            var pumpErr = PumpAsync(process.StandardError, Sink, ct);

            try
            {
                await process.WaitForExitAsync(ct);
                await Task.WhenAll(pumpOut, pumpErr); // flush any buffered lines
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
                throw;
            }

            var output = string.Join("\n", collected);
            return new ActionExecutionResult
            {
                Success = process.ExitCode == 0,
                StatusCode = process.ExitCode,
                Message = process.ExitCode == 0 ? "Process completed successfully" : $"Process exited with code {process.ExitCode}",
                Output = output.Length > 2000 ? output[..2000] + "..." : output
            };
        }
        catch (OperationCanceledException)
        {
            throw;
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

    private static async Task PumpAsync(TextReader reader, Action<string> sink, CancellationToken ct)
    {
        string? line;
        while ((line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
            sink(line);
    }

    /// <summary>Parameters first, then content/entry references, then secrets.</summary>
    private string ResolveAll(string input, IReadOnlyDictionary<string, string>? parameters, ActionContext? context)
        => _secretResolver.Resolve(_contentResolver.Resolve(_parameterResolver.Resolve(input, parameters), context));
}
