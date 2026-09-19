using System.Diagnostics;
using Xunit.Abstractions;

namespace OcwOffline.Tests;

// Standards-compliance: the v0.4 prose doctrine (no em-dashes, no "--" dodges,
// no diary comments) is enforced by tools/lint/prose_lint.py. This test runs
// the script over the repo root and fails on any violation, so both
// `dotnet test` and CI catch regressions.
public class ProseLintComplianceTests
{
    private readonly ITestOutputHelper _output;

    public ProseLintComplianceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ProseLint_WhenRunOverRepoRoot_ExitsZero()
    {
        var repoRoot = FindRepoRootContainingLintScript()
            ?? throw new InvalidOperationException(
                "Could not locate repo root: tools/lint/prose_lint.py not found " +
                "walking up from " + TestAssemblyDirectory() + ".");

        if (!Python3Available())
        {
            _output.WriteLine("SKIPPED: python3 not found on PATH; prose lint not run.");
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = "python3",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add(Path.Combine(repoRoot, "tools", "lint", "prose_lint.py"));
        psi.ArgumentList.Add(repoRoot);

        using var proc = Process.Start(psi);
        Assert.NotNull(proc);
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit();
        Assert.True(proc.ExitCode == 0,
            $"prose_lint.py reported violations:{Environment.NewLine}{stdout}{stderr}");
    }

    private static string? FindRepoRootContainingLintScript()
    {
        // Walk up from the test assembly's directory, not AppContext.BaseDirectory:
        // under the xUnit console runner the base directory is the runner's own
        // folder, which sits outside the repo.
        var dir = TestAssemblyDirectory() is string start ? new DirectoryInfo(start) : null;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "tools", "lint", "prose_lint.py")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    private static string? TestAssemblyDirectory() =>
        Path.GetDirectoryName(typeof(ProseLintComplianceTests).Assembly.Location);

    private static bool Python3Available()
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "python3",
                Arguments = "--version",
                RedirectStandardOutput = true,
                UseShellExecute = false,
            });
            if (proc is null)
                return false;
            proc.WaitForExit(5000);
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
