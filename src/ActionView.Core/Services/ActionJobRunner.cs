using System.Collections.Concurrent;
using System.Diagnostics;
using ActionView.Core.Models;
using Microsoft.Extensions.Logging;

namespace ActionView.Core.Services;

/// <summary>
/// Runs action commands as background jobs so long-running commands don't block
/// the request or the UI. Each job runs on a background task with its own
/// cancellation source; CLI output is streamed line-by-line; concurrency is
/// bounded; an optional default timeout auto-fails hung jobs. Lifecycle is
/// surfaced through <see cref="JobStarted"/>/<see cref="JobProgress"/>/<see
/// cref="JobFinished"/> so the API layer can broadcast (SignalR), persist
/// (audit log), and apply post-action behavior without this service depending
/// on those concerns.
/// </summary>
public sealed class ActionJobRunner : IDisposable
{
    private readonly ActionExecutor _executor;
    private readonly ILogger<ActionJobRunner> _logger;
    private readonly ConcurrentDictionary<string, ActionJob> _jobs = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cts = new();
    private readonly SemaphoreSlim _concurrency;
    private readonly int _outputTailLines;
    private readonly int _defaultTimeoutSeconds;
    private const int MaxRetainedJobs = 400;

    /// <summary>Raised when a job transitions to running.</summary>
    public event Action<ActionJob>? JobStarted;

    /// <summary>Raised for each streamed output line (jobId + line).</summary>
    public event Action<ActionJob, string>? JobProgress;

    /// <summary>Raised when a job reaches a terminal state (succeeded/failed/cancelled).</summary>
    public event Action<ActionJob>? JobFinished;

    public ActionJobRunner(ActionExecutor executor, ActionsConfig config, ILogger<ActionJobRunner> logger)
    {
        _executor = executor;
        _logger = logger;
        _concurrency = new SemaphoreSlim(Math.Max(1, config.MaxConcurrentJobs));
        _outputTailLines = Math.Max(10, config.OutputTailLines);
        _defaultTimeoutSeconds = Math.Max(0, config.DefaultTimeoutSeconds);
    }

    /// <summary>
    /// Registers and starts a pre-built job (the caller supplies entry/action
    /// metadata + post-behavior). Returns immediately; the command runs in the
    /// background.
    /// </summary>
    public ActionJob Start(
        ActionJob job,
        ActionCommand command,
        IReadOnlyDictionary<string, string>? parameters,
        ActionContext? context)
    {
        _jobs[job.Id] = job;
        var cts = new CancellationTokenSource();
        _cts[job.Id] = cts;
        Prune();
        _ = Task.Run(() => RunAsync(job, command, parameters, context, cts));
        return job;
    }

    private async Task RunAsync(
        ActionJob job,
        ActionCommand command,
        IReadOnlyDictionary<string, string>? parameters,
        ActionContext? context,
        CancellationTokenSource cts)
    {
        await _concurrency.WaitAsync().ConfigureAwait(false);
        var sw = Stopwatch.StartNew();
        try
        {
            using var timeoutCts = _defaultTimeoutSeconds > 0
                ? CancellationTokenSource.CreateLinkedTokenSource(cts.Token)
                : null;
            timeoutCts?.CancelAfter(TimeSpan.FromSeconds(_defaultTimeoutSeconds));
            var ct = timeoutCts?.Token ?? cts.Token;

            job.Status = ActionJobStatus.Running;
            job.StartedAt = DateTimeOffset.UtcNow;
            JobStarted?.Invoke(job);

            var result = await _executor
                .ExecuteStreamingAsync(command, parameters, context, line => AppendOutput(job, line), ct)
                .ConfigureAwait(false);

            job.Status = result.Success ? ActionJobStatus.Succeeded : ActionJobStatus.Failed;
            job.ExitCode = result.StatusCode;
            job.Message = result.Message;
        }
        catch (OperationCanceledException)
        {
            if (cts.IsCancellationRequested)
            {
                job.Status = ActionJobStatus.Cancelled;
                job.Message = "Cancelled by user";
            }
            else
            {
                job.Status = ActionJobStatus.Failed;
                job.Message = $"Timed out after {_defaultTimeoutSeconds}s";
            }
        }
        catch (Exception ex)
        {
            job.Status = ActionJobStatus.Failed;
            job.Message = ex.Message;
            _logger.LogError(ex, "Action job {JobId} failed", job.Id);
        }
        finally
        {
            sw.Stop();
            job.FinishedAt = DateTimeOffset.UtcNow;
            job.DurationMs = sw.ElapsedMilliseconds;
            _concurrency.Release();
            _cts.TryRemove(job.Id, out _);
            cts.Dispose();
            JobFinished?.Invoke(job);
        }
    }

    private void AppendOutput(ActionJob job, string line)
    {
        lock (job.OutputTail)
        {
            job.OutputTail.Add(line);
            if (job.OutputTail.Count > _outputTailLines)
                job.OutputTail.RemoveRange(0, job.OutputTail.Count - _outputTailLines);
        }
        JobProgress?.Invoke(job, line);
    }

    /// <summary>Requests cancellation of a running job. Returns false if the id is unknown/finished.</summary>
    public bool Cancel(string id)
    {
        if (_cts.TryGetValue(id, out var cts))
        {
            try { cts.Cancel(); } catch { /* already disposed */ }
            return true;
        }
        return false;
    }

    /// <summary>Returns a job snapshot, or null if unknown (e.g. lost to a restart).</summary>
    public ActionJob? Get(string id) => _jobs.TryGetValue(id, out var job) ? job : null;

    /// <summary>Returns pending/running jobs, optionally filtered to one entry.</summary>
    public IReadOnlyList<ActionJob> Active(string? entryId = null)
        => _jobs.Values
            .Where(j => j.Status is ActionJobStatus.Pending or ActionJobStatus.Running)
            .Where(j => entryId is null || j.EntryId == entryId)
            .OrderBy(j => j.StartedAt)
            .ToList();

    // Keep memory bounded by evicting the oldest finished jobs once we exceed the cap.
    private void Prune()
    {
        if (_jobs.Count <= MaxRetainedJobs) return;
        var finished = _jobs.Values
            .Where(j => j.FinishedAt is not null)
            .OrderBy(j => j.FinishedAt)
            .Take(_jobs.Count - MaxRetainedJobs)
            .Select(j => j.Id)
            .ToList();
        foreach (var id in finished)
            _jobs.TryRemove(id, out _);
    }

    public void Dispose()
    {
        foreach (var cts in _cts.Values)
        {
            try { cts.Cancel(); cts.Dispose(); } catch { /* best effort */ }
        }
        _cts.Clear();
        _concurrency.Dispose();
    }
}
