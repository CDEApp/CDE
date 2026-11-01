using System;
using System.Collections.Generic;
using System.Linq;
using cdeAppCore.Shell;
using NUnit.Framework;

namespace cdeAppCoreTest;

[TestFixture]
public class CommandTokensSplitTests
{
    [Test]
    public void PathWithSpacesAndQuotes_StaysOneArgument()
    {
        var argv = CommandTokens.SplitAndSubstitute("--open \"{path}\" -x '{filename}'", "/d/my \"odd\" file's.txt");

        Assert.That(argv, Is.EqualTo(new[] { "--open", "/d/my \"odd\" file's.txt", "-x", "my \"odd\" file's.txt" }));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void BlankTemplate_IsNoArguments(string template)
    {
        Assert.That(CommandTokens.SplitAndSubstitute(template, "/a"), Is.Empty);
    }

    [Test]
    public void EmptyQuotes_AreAnEmptyArgument()
    {
        Assert.That(CommandTokens.SplitAndSubstitute("a \"\" b", "/x"), Is.EqualTo(new[] { "a", "", "b" }));
    }
}

internal sealed class FakeRunner : ICommandRunner
{
    public List<(string File, string[] Args, bool Wait)> Calls { get; } = [];
    public Func<string, CommandRunResult> Respond { get; set; } = _ => new CommandRunResult(true);

    public CommandRunResult Run(string fileName, IReadOnlyList<string> arguments, bool wait)
    {
        Calls.Add((fileName, arguments.ToArray(), wait));
        return Respond(fileName);
    }
}

[TestFixture]
public class LinuxShellActionsTests
{
    private FakeRunner _runner;

    [SetUp]
    public void SetUp() => _runner = new FakeRunner();

    private LinuxShellActions Create(ShellCommandOverrides overrides = null, params CustomCommandOptions[] commands) =>
        new(commands, _runner, overrides);

    [Test]
    public void Open_SpawnsXdgOpenWithThePathAsOneArgument()
    {
        var result = Create().Open("/d/a b.txt");

        Assert.That(result.IsOk);
        Assert.That(_runner.Calls, Is.EqualTo(new[] { ("xdg-open", new[] { "/d/a b.txt" }, false) }));
    }

    [Test]
    public void Open_WhenXdgOpenIsMissing_Fails()
    {
        _runner.Respond = _ => new CommandRunResult(false, "could not start xdg-open");

        var result = Create().Open("/d/a.txt");

        Assert.That(result.Status, Is.EqualTo(ShellStatus.Failed));
        Assert.That(result.Message, Does.Contain("xdg-open"));
    }

    [Test]
    public void Explore_CallsShowItemsWithAnEncodedFileUri()
    {
        var result = Create().Explore("/d/a b,c%.txt");

        Assert.That(result.IsOk);
        Assert.That(_runner.Calls, Has.Count.EqualTo(1));
        var (file, args, wait) = _runner.Calls[0];
        Assert.That(file, Is.EqualTo("dbus-send"));
        Assert.That(wait);
        Assert.That(args, Is.EqualTo(new[]
        {
            "--session", "--print-reply", "--reply-timeout=2000", "--dest=org.freedesktop.FileManager1",
            "--type=method_call", "/org/freedesktop/FileManager1", "org.freedesktop.FileManager1.ShowItems",
            "array:string:file:///d/a%20b%2Cc%25.txt", "string:"
        }));
    }

    [Test]
    public void Explore_WhenDbusFails_FallsBackToOpeningTheParentFolder()
    {
        _runner.Respond = file => file == "dbus-send" ? new CommandRunResult(false, "no service") : new CommandRunResult(true);

        var result = Create().Explore("/d/sub/a.txt");

        Assert.That(result.IsOk);
        Assert.That(_runner.Calls.Select(c => c.File), Is.EqualTo(new[] { "dbus-send", "xdg-open" }));
        Assert.That(_runner.Calls[1].Args, Is.EqualTo(new[] { "/d/sub" }));
    }

    [Test]
    public void Explore_WhenDbusAndFallbackFail_Fails()
    {
        _runner.Respond = _ => new CommandRunResult(false, "boom");

        Assert.That(Create().Explore("/d/a.txt").Status, Is.EqualTo(ShellStatus.Failed));
    }

    [Test]
    public void Properties_CallsShowItemPropertiesAndHasNoFallback()
    {
        _runner.Respond = _ => new CommandRunResult(false, "no service");

        var result = Create().ShowProperties("/d/a.txt");

        Assert.That(result.Status, Is.EqualTo(ShellStatus.Failed));
        Assert.That(result.Message, Does.Contain("unavailable"));
        Assert.That(_runner.Calls, Has.Count.EqualTo(1));
        Assert.That(_runner.Calls[0].Args, Does.Contain("org.freedesktop.FileManager1.ShowItemProperties"));
    }

    [Test]
    public void Override_ReplacesTheBuiltInVerbOnly()
    {
        var overrides = new ShellCommandOverrides
        {
            OpenCommand = new CustomCommandOptions { Command = "my-open", Arguments = "--file \"{path}\"" }
        };
        var actions = Create(overrides);

        Assert.That(actions.Open("/d/a b.txt").IsOk);
        actions.Explore("/d/a.txt");

        Assert.That(_runner.Calls[0], Is.EqualTo(("my-open", new[] { "--file", "/d/a b.txt" }, false)));
        Assert.That(_runner.Calls[1].File, Is.EqualTo("dbus-send"));
    }

    [Test]
    public void Override_FailureIsNotFollowedByTheBuiltIn()
    {
        _runner.Respond = _ => new CommandRunResult(false, "nope");
        var overrides = new ShellCommandOverrides
        {
            ExploreCommand = new CustomCommandOptions { Command = "my-explore", Arguments = "{dir}" }
        };

        var result = Create(overrides).Explore("/d/sub/a.txt");

        Assert.That(result.Status, Is.EqualTo(ShellStatus.Failed));
        Assert.That(_runner.Calls, Has.Count.EqualTo(1));
    }

    [Test]
    public void CustomCommand_RunsWithSubstitutedArgv()
    {
        var actions = Create(null, new CustomCommandOptions { Command = "edit", Arguments = "-d {dir} \"{path}\"" });

        var result = actions.RunCustomCommand(0, "/d/my file.txt");

        Assert.That(result.IsOk);
        Assert.That(_runner.Calls[0], Is.EqualTo(("edit", new[] { "-d", System.IO.Path.GetDirectoryName("/d/my file.txt"), "/d/my file.txt" }, false)));
    }

    [TestCase(-1)]
    [TestCase(0)]
    public void CustomCommand_IndexOutOfRange_Fails(int index)
    {
        Assert.That(Create().RunCustomCommand(index, "/d/a").Status, Is.EqualTo(ShellStatus.Failed));
        Assert.That(_runner.Calls, Is.Empty);
    }

    [Test]
    public void CustomCommand_WithoutProgram_Fails()
    {
        Assert.That(Create().RunCustomCommand(new CustomCommandOptions(), "/d/a").Status, Is.EqualTo(ShellStatus.Failed));
        Assert.That(Create().RunCustomCommand((CustomCommandOptions)null, "/d/a").Status, Is.EqualTo(ShellStatus.Failed));
    }
}

[TestFixture]
public class MacShellActionsTests
{
    private FakeRunner _runner;

    [SetUp]
    public void SetUp() => _runner = new FakeRunner();

    [Test]
    public void Open_RunsOpenWithThePath()
    {
        var result = new MacShellActions([], _runner).Open("/d/a b.txt");

        Assert.That(result.IsOk);
        Assert.That(_runner.Calls, Is.EqualTo(new[] { ("open", new[] { "/d/a b.txt" }, true) }));
    }

    [Test]
    public void Explore_RunsOpenReveal()
    {
        new MacShellActions([], _runner).Explore("/d/a.txt");

        Assert.That(_runner.Calls, Is.EqualTo(new[] { ("open", new[] { "-R", "/d/a.txt" }, true) }));
    }

    [Test]
    public void Properties_AsksFinderThroughOsascriptWithThePathAsAnArgument()
    {
        new MacShellActions([], _runner).ShowProperties("/d/a \"q\".txt");

        var (file, args, _) = _runner.Calls[0];
        Assert.That(file, Is.EqualTo("osascript"));
        Assert.That(args[0], Is.EqualTo("-e"));
        Assert.That(args[1], Does.Contain("information window"));
        Assert.That(args[1], Does.Not.Contain("a \"q\""));
        Assert.That(args[2], Is.EqualTo("/d/a \"q\".txt"));
    }

    [Test]
    public void Failure_IsReportedWithTheReason()
    {
        _runner.Respond = _ => new CommandRunResult(false, "open exited with code 1");

        var result = new MacShellActions([], _runner).Explore("/d/a.txt");

        Assert.That(result.Status, Is.EqualTo(ShellStatus.Failed));
        Assert.That(result.Message, Is.EqualTo("open exited with code 1"));
    }

    [Test]
    public void Override_ReplacesTheVerb()
    {
        var overrides = new ShellCommandOverrides { PropertiesCommand = new CustomCommandOptions { Command = "info", Arguments = "{path}" } };

        new MacShellActions([], _runner, overrides).ShowProperties("/d/a.txt");

        Assert.That(_runner.Calls, Is.EqualTo(new[] { ("info", new[] { "/d/a.txt" }, false) }));
    }
}

[TestFixture]
public class ProcessCommandRunnerTests
{
    [Test]
    public void MissingProgram_ReportsItDidNotStart()
    {
        var result = new ProcessCommandRunner().Run("cde-no-such-program-xyz", [], wait: false);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Does.Contain("cde-no-such-program-xyz"));
    }

    [Test]
    public void WaitedCommand_SuccessIsExitCodeZero()
    {
        var (file, ok, bad) = OperatingSystem.IsWindows()
            ? ("cmd.exe", new[] { "/c", "exit 0" }, new[] { "/c", "exit 3" })
            : ("sh", new[] { "-c", "exit 0" }, new[] { "-c", "exit 3" });
        var runner = new ProcessCommandRunner();

        Assert.That(runner.Run(file, ok, wait: true).Success);
        var failed = runner.Run(file, bad, wait: true);
        Assert.That(failed.Success, Is.False);
        Assert.That(failed.Error, Does.Contain("code 3"));
    }
}
