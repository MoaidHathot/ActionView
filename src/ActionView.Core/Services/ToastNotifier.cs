using System.Diagnostics;
using ActionView.Core.Models;
using Microsoft.Extensions.Logging;

namespace ActionView.Core.Services;

/// <summary>
/// Sends Windows toast notifications via Palantir using <c>dnx palantir</c>.
/// dnx is the .NET 10 tool execution script (like npx for Node.js) that runs
/// .NET tools without requiring a global install.
/// </summary>
public sealed class ToastNotifier
{
    private readonly AppConfig _config;
    private readonly ILogger<ToastNotifier> _logger;

    public ToastNotifier(AppConfig config, ILogger<ToastNotifier> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Whether toast notifications are effectively enabled.
    /// Both the global notification flag and the Palantir flag must be true.
    /// </summary>
    public bool IsEnabled => _config.Notifications.Enabled && _config.Notifications.Palantir.Enabled;

    /// <summary>
    /// Send a toast notification for each new entry.
    /// </summary>
    public void NotifyEntries(List<Entry> entries)
    {
        if (!IsEnabled)
            return;

        foreach (var entry in entries)
        {
            try
            {
                SendToast(entry);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send toast notification for entry {Id}", entry.Id);
            }
        }
    }

    private void SendToast(Entry entry)
    {
        var palantir = _config.Notifications.Palantir;

        var args = BuildArguments(entry, palantir);

        _logger.LogDebug("Sending toast via dnx palantir: {Args}", args);

        var psi = new ProcessStartInfo
        {
            FileName = "dnx",
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(psi);
        if (process is null)
        {
            _logger.LogWarning("Failed to start dnx palantir process");
            return;
        }

        // Don't block the caller; fire and forget with a timeout
        _ = Task.Run(async () =>
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                await process.WaitForExitAsync(cts.Token);

                if (process.ExitCode != 0)
                {
                    var stderr = await process.StandardError.ReadToEndAsync(cts.Token);
                    _logger.LogWarning(
                        "Palantir exited with code {ExitCode} for entry {Id}: {Error}",
                        process.ExitCode, entry.Id, stderr);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Palantir process timed out for entry {Id}", entry.Id);
                try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
            }
        });
    }

    internal static string BuildArguments(Entry entry, PalantirConfig config)
    {
        var args = new List<string>
        {
            "palantir",
            "-t", Quote(entry.Title)
        };

        // Compose message from subtitle or type + severity
        var message = entry.Subtitle ?? $"[{entry.Severity}] {entry.Type}";
        args.Add("-m");
        args.Add(Quote(message));

        // Add tags as the third text line if present
        if (entry.Tags.Count > 0)
        {
            args.Add("-b");
            args.Add(Quote(string.Join(", ", entry.Tags)));
        }

        // Attribution
        if (!string.IsNullOrWhiteSpace(config.Attribution))
        {
            args.Add("--attribution");
            args.Add(Quote(config.Attribution));
        }

        // Launch URL
        if (!string.IsNullOrWhiteSpace(config.LaunchUrl))
        {
            args.Add("--launch");
            args.Add(Quote(config.LaunchUrl));
        }

        // Duration
        if (!string.IsNullOrWhiteSpace(config.Duration))
        {
            args.Add("--duration");
            args.Add(config.Duration);
        }

        // Image
        if (!string.IsNullOrWhiteSpace(config.Image))
        {
            args.Add("-i");
            args.Add(Quote(config.Image));
        }

        // Hero image
        if (!string.IsNullOrWhiteSpace(config.HeroImage))
        {
            args.Add("--hero-image");
            args.Add(Quote(config.HeroImage));
        }

        // Audio
        if (!string.IsNullOrWhiteSpace(config.Audio))
        {
            args.Add("--audio");
            args.Add(config.Audio);
        }

        // Silent
        if (config.Silent)
        {
            args.Add("--silent");
        }

        // Scenario
        if (!string.IsNullOrWhiteSpace(config.Scenario))
        {
            args.Add("--scenario");
            args.Add(config.Scenario);
        }

        // Always suppress Palantir's own console output
        args.Add("-q");

        return string.Join(' ', args);
    }

    private static string Quote(string value)
    {
        // Escape any embedded double-quotes and wrap in double-quotes
        return $"\"{value.Replace("\"", "\\\"")}\"";
    }
}
