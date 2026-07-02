using System.Diagnostics;
using System.Text.Json;

namespace ActionView.Tests;

/// <summary>
/// End-to-end guard for the --group-id empty-argument corruption the consumer reported:
/// runs the actual built CLI so the System.CommandLine parsing path is exercised, not just
/// the classifier. Covers the shell-dropped-empty swallow, the empty-omit path, and a normal
/// group id.
/// </summary>
public class CliAddIntegrationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;
    private const string EntryJson = """{"type":"deploy","source":"cli","title":"Ship"}""";

    public CliAddIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"actionview_cli_it_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "actionview.json");
        File.WriteAllText(_configPath, """{ "dataDirectory": "data" }""");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private static string CliDllPath()
    {
        // .../src/ActionView.Tests/bin/<Config>/<tfm>/ActionView.Tests.dll
        // -> .../src/ActionView.Cli/bin/<Config>/<tfm>/ActionView.Cli.dll
        var dir = new DirectoryInfo(Path.GetDirectoryName(typeof(CliAddIntegrationTests).Assembly.Location)!);
        var tfm = dir.Name;
        var config = dir.Parent!.Name;
        var src = dir.Parent!.Parent!.Parent!.Parent!.FullName;
        return Path.Combine(src, "ActionView.Cli", "bin", config, tfm, "ActionView.Cli.dll");
    }

    private (int ExitCode, string StdErr) RunAdd(params string[] extraArgs)
    {
        var dll = CliDllPath();
        Assert.True(File.Exists(dll), $"CLI assembly not found at {dll}");

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = _tempDir
        };
        // ArgumentList preserves empty-string arguments verbatim, so we can simulate both the
        // "empty preserved" and (by omitting the empty) the "empty dropped" argv shapes.
        psi.ArgumentList.Add(dll);
        psi.ArgumentList.Add("add");
        foreach (var a in extraArgs) psi.ArgumentList.Add(a);
        psi.ArgumentList.Add("--config");
        psi.ArgumentList.Add(_configPath);

        using var proc = Process.Start(psi)!;
        var stderr = proc.StandardError.ReadToEnd();
        _ = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit(30_000);
        return (proc.ExitCode, stderr);
    }

    private string? InboxEntry()
    {
        var inbox = Path.Combine(_tempDir, "data", "inbox");
        if (!Directory.Exists(inbox)) return null;
        var file = Directory.EnumerateFiles(inbox, "*.json").FirstOrDefault();
        return file is null ? null : File.ReadAllText(file);
    }

    [Fact]
    public void Add_GroupIdSwallowsFollowingFlag_ErrorsAndWritesNothing()
    {
        // Shell dropped the empty "" so argv collapsed to `--group-id --wait`.
        var (exit, stderr) = RunAdd("-j", EntryJson, "--group-id", "--wait");

        Assert.NotEqual(0, exit);
        Assert.Contains("looks like a flag", stderr);
        Assert.Null(InboxEntry()); // no corrupt entry written
    }

    [Fact]
    public void Add_EmptyGroupId_OmitsFieldAndSucceeds()
    {
        // Empty preserved: `--group-id "" --wait`.
        var (exit, _) = RunAdd("-j", EntryJson, "--group-id", "", "--wait");

        Assert.Equal(0, exit);
        var entry = InboxEntry();
        Assert.NotNull(entry);
        using var doc = JsonDocument.Parse(entry!);
        Assert.False(doc.RootElement.TryGetProperty("groupId", out _)); // never injected as ""
    }

    [Fact]
    public void Add_ValidGroupId_IsApplied()
    {
        var (exit, _) = RunAdd("-j", EntryJson, "--group-id", "ci-1847", "--wait");

        Assert.Equal(0, exit);
        var entry = InboxEntry();
        Assert.NotNull(entry);
        using var doc = JsonDocument.Parse(entry!);
        Assert.Equal("ci-1847", doc.RootElement.GetProperty("groupId").GetString());
    }
}
