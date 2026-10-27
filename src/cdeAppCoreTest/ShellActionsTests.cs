using System.Collections.Generic;
using System.Runtime.Versioning;
using cdeAppCore.Shell;
using NSubstitute;
using NUnit.Framework;
using Serilog;

namespace cdeAppCoreTest;

[TestFixture]
public class CommandTokensTests
{
    [Test]
    public void Substitute_ReplacesPathDirAndFilename()
    {
        var result = CommandTokens.Substitute("{path}|{dir}|{filename}", "/data/file.txt");

        Assert.That(result, Does.StartWith("/data/file.txt|"));
        Assert.That(result, Does.EndWith("|file.txt"));
        Assert.That(result, Does.Contain("data"));
    }

    [Test]
    public void Substitute_RepeatedTokens_AreAllReplaced()
    {
        Assert.That(CommandTokens.Substitute("{filename} {filename}", "x/y.txt"), Is.EqualTo("y.txt y.txt"));
    }

    [TestCase(null)]
    [TestCase("")]
    public void Substitute_NullOrEmptyTemplate_IsReturnedUnchanged(string template)
    {
        Assert.That(CommandTokens.Substitute(template, "a"), Is.EqualTo(template));
    }

    [Test]
    public void Substitute_NoTokens_IsReturnedUnchanged()
    {
        Assert.That(CommandTokens.Substitute("--flag", "a"), Is.EqualTo("--flag"));
    }
}

[TestFixture]
public class NoopShellActionsTests
{
    [Test]
    public void NullCommandList_BecomesEmpty()
    {
        Assert.That(new NoopShellActions(null).CustomCommands, Is.Empty);
    }

    [Test]
    public void CustomCommands_AreExposed()
    {
        var commands = new List<CustomCommandOptions> { new() { Command = "x" } };
        Assert.That(new NoopShellActions(commands).CustomCommands, Is.SameAs(commands));
    }

    [Test]
    public void EveryAction_IsALoggedNoOp()
    {
        var logger = Substitute.For<ILogger>();
        var actions = new NoopShellActions([], logger);

        actions.Open("p");
        actions.Explore("p");
        actions.ShowProperties("p");
        actions.RunCustomCommand(0, "p");
        actions.RunCustomCommand(new CustomCommandOptions { Command = "x" }, "p");

        logger.Received(5).Information(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public void ActionsWithoutLogger_DoNotThrow()
    {
        var actions = new NoopShellActions([]);

        Assert.DoesNotThrow(() =>
        {
            actions.Open("p");
            actions.Explore("p");
            actions.ShowProperties("p");
            actions.RunCustomCommand(0, "p");
        });
    }
}

[TestFixture]
[SupportedOSPlatform("windows")]
[Platform("Win")]
public class WindowsShellActionsTests
{
    [Test]
    public void NullCommandList_BecomesEmpty()
    {
        Assert.That(new WindowsShellActions(null, Substitute.For<ILogger>()).CustomCommands, Is.Empty);
    }

    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(5)]
    public void RunCustomCommand_IndexOutOfRange_DoesNothing(int index)
    {
        var actions = new WindowsShellActions([], Substitute.For<ILogger>());
        Assert.DoesNotThrow(() => actions.RunCustomCommand(index, "p"));
    }

    [Test]
    public void RunCustomCommand_NullOrBlankCommand_DoesNothing()
    {
        var actions = new WindowsShellActions([], Substitute.For<ILogger>());

        Assert.DoesNotThrow(() => actions.RunCustomCommand((CustomCommandOptions)null, "p"));
        Assert.DoesNotThrow(() => actions.RunCustomCommand(new CustomCommandOptions { Command = "" }, "p"));
    }

    [Test]
    public void RunCustomCommand_MissingExecutable_IsLoggedNotThrown()
    {
        var logger = Substitute.For<ILogger>();
        var actions = new WindowsShellActions([], logger);

        actions.RunCustomCommand(
            new CustomCommandOptions { Command = @"C:\does-not-exist\nope-cde-test.exe" }, @"C:\x\y.txt");

        logger.Received(1).Warning(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public void RunCustomCommand_ByIndex_StartsConfiguredCommand()
    {
        var command = new CustomCommandOptions { Command = "cmd.exe", Arguments = "/c exit 0" };
        var actions = new WindowsShellActions([command], Substitute.For<ILogger>());

        Assert.DoesNotThrow(() => actions.RunCustomCommand(0, @"C:\x\y.txt"));
    }
}
