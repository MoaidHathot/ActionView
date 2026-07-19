using ActionView.Core.Models;
using ActionView.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ActionView.Tests;

public class ActionJobRunnerTests
{
    private static ActionExecutor CreateExecutor() => new(
        new ParameterResolver(),
        new ContentReferenceResolver(),
        new SecretResolver(new AppConfig()),
        new HttpClient(),
        NullLogger<ActionExecutor>.Instance);

    private static ActionJobRunner CreateRunner(ActionsConfig? config = null) =>
        new(CreateExecutor(), config ?? new ActionsConfig(), NullLogger<ActionJobRunner>.Instance);

    private static ActionCommand Cli(params string[] args) =>
        new() { Type = CommandType.Cli, Program = "cmd", Args = ["/c", .. args] };

    private static async Task<ActionJob> RunToCompletion(ActionJobRunner runner, ActionJob job, ActionCommand command)
    {
        var tcs = new TaskCompletionSource<ActionJob>(TaskCreationOptions.RunContinuationsAsynchronously);
        runner.JobFinished += j => { if (j.Id == job.Id) tcs.TrySetResult(j); };
        runner.Start(job, command, null, null);
        return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }

    private static ActionJob NewJob() => new() { EntryId = "e1", ActionLabel = "Run" };

    [Fact]
    public async Task Succeeds_AndCapturesOutput()
    {
        if (!OperatingSystem.IsWindows()) return;
        var runner = CreateRunner();
        var job = await RunToCompletion(runner, NewJob(), Cli("echo hello-world"));

        Assert.Equal(ActionJobStatus.Succeeded, job.Status);
        Assert.Equal(0, job.ExitCode);
        Assert.Contains(job.OutputTail, l => l.Contains("hello-world"));
        Assert.NotNull(job.DurationMs);
    }

    [Fact]
    public async Task NonZeroExit_MarksFailed()
    {
        if (!OperatingSystem.IsWindows()) return;
        var runner = CreateRunner();
        var job = await RunToCompletion(runner, NewJob(), Cli("exit 3"));

        Assert.Equal(ActionJobStatus.Failed, job.Status);
        Assert.Equal(3, job.ExitCode);
    }

    [Fact]
    public async Task Cancel_KillsAndMarksCancelled()
    {
        if (!OperatingSystem.IsWindows()) return;
        var runner = CreateRunner();
        var job = NewJob();

        var tcs = new TaskCompletionSource<ActionJob>(TaskCreationOptions.RunContinuationsAsynchronously);
        runner.JobFinished += j => { if (j.Id == job.Id) tcs.TrySetResult(j); };

        // Long-running: ping loopback ~20s.
        runner.Start(job, Cli("ping -n 20 127.0.0.1"), null, null);
        await Task.Delay(600);
        Assert.True(runner.Cancel(job.Id));

        var finished = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(ActionJobStatus.Cancelled, finished.Status);
    }

    [Fact]
    public async Task Timeout_MarksFailed()
    {
        if (!OperatingSystem.IsWindows()) return;
        var runner = CreateRunner(new ActionsConfig { DefaultTimeoutSeconds = 1 });
        var job = await RunToCompletion(runner, NewJob(), Cli("ping -n 20 127.0.0.1"));

        Assert.Equal(ActionJobStatus.Failed, job.Status);
        Assert.Contains("imed out", job.Message);
    }

    [Fact]
    public async Task RaisesStarted_BeforeFinished()
    {
        if (!OperatingSystem.IsWindows()) return;
        var runner = CreateRunner();
        var job = NewJob();
        var started = false;
        runner.JobStarted += j => { if (j.Id == job.Id) started = true; };

        await RunToCompletion(runner, job, Cli("echo hi"));
        Assert.True(started);
    }

    [Fact]
    public void Cancel_UnknownJob_ReturnsFalse()
    {
        var runner = CreateRunner();
        Assert.False(runner.Cancel("nope"));
        Assert.Null(runner.Get("nope"));
    }
}
