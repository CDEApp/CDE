using System.Collections.Generic;
using System.IO;
using cdeApi;
using cdeAppCore.Shell;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace cdeApiTest;

/// <summary>Drives the <see cref="ShellEndpoints"/> handlers against a real session and a recording shell.</summary>
[TestFixture]
public class ShellEndpointsTests
{
    private TestCatalog _catalog;
    private RecordingShell _shell;

    [SetUp]
    public void SetUp()
    {
        _catalog = new TestCatalog();
        _shell = new RecordingShell();
    }

    [TearDown]
    public void TearDown() => _catalog.Dispose();

    private IResult Run(string @ref, string action, int? commandId = null) =>
        ShellEndpoints.Run(new ShellRequest { Ref = @ref, Action = action, CommandId = commandId }, _catalog.Session, _shell);

    private static int? Status(IResult result) => ((IStatusCodeHttpResult)result).StatusCode;

    private static object Value(IResult result) => ((IValueHttpResult)result).Value;

    [Test]
    public void Null_request_is_a_bad_ref()
    {
        var result = ShellEndpoints.Run(null, _catalog.Session, _shell);

        Assert.That(Status(result), Is.EqualTo(400));
        Assert.That(Value(result), Is.EqualTo("bad ref"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("nope")]
    public void Malformed_ref_is_a_bad_ref(string @ref)
    {
        var result = Run(@ref, "open");

        Assert.That(Status(result), Is.EqualTo(400));
        Assert.That(Value(result), Is.EqualTo("bad ref"));
        Assert.That(_shell.Calls, Is.Empty);
    }

    [Test]
    public void Entry_missing_from_disk_is_refused_without_calling_the_shell()
    {
        var @ref = _catalog.RefOf("a.txt");
        File.Delete(Path.Combine(_catalog.Data, "a.txt"));

        var result = Run(@ref, "open");

        Assert.That(Status(result), Is.EqualTo(400));
        Assert.That(Value(result), Is.EqualTo("entry not present on this filesystem"));
        Assert.That(_shell.Calls, Is.Empty);
    }

    [TestCase("open", "Open")]
    [TestCase("explore", "Explore")]
    [TestCase("properties", "ShowProperties")]
    public void Action_runs_against_the_resolved_path(string action, string expectedCall)
    {
        var result = Run(_catalog.RefOf("a.txt"), action);

        Assert.That(Status(result), Is.EqualTo(200));
        Assert.That(_shell.Calls, Is.EqualTo(new[] { $"{expectedCall} {Path.Combine(_catalog.Data, "a.txt")}" }));
    }

    [TestCase(1, 1)]
    [TestCase(null, -1)]
    public void Custom_action_runs_the_command_by_id(int? commandId, int expectedIndex)
    {
        var result = Run(_catalog.RefOf("sub"), "custom", commandId);

        Assert.That(Status(result), Is.EqualTo(200));
        Assert.That(_shell.Calls, Is.EqualTo(new[] { $"Custom {expectedIndex} {Path.Combine(_catalog.Data, "sub")}" }));
    }

    [TestCase("delete")]
    [TestCase(null)]
    public void Unknown_action_is_refused(string action)
    {
        var result = Run(_catalog.RefOf("a.txt"), action);

        Assert.That(Status(result), Is.EqualTo(400));
        Assert.That(Value(result), Is.EqualTo("unknown action"));
        Assert.That(_shell.Calls, Is.Empty);
    }

    [Test]
    public void Commands_lists_the_configured_commands_by_index()
    {
        var result = ShellEndpoints.Commands(_shell);

        var commands = (List<ShellCommandInfo>)Value(result);
        Assert.That(commands.ConvertAll(c => (c.Id, c.Label)), Is.EqualTo(new[] { (0, "Editor"), (1, "Hasher") }));
    }

    private sealed class RecordingShell : IShellActions
    {
        public List<string> Calls { get; } = [];

        public IReadOnlyList<CustomCommandOptions> CustomCommands { get; } =
        [
            new() { Label = "Editor", Command = "edit.exe" },
            new() { Label = "Hasher", Command = "hash.exe" },
        ];

        public void Open(string fullPath) => Calls.Add($"Open {fullPath}");
        public void Explore(string fullPath) => Calls.Add($"Explore {fullPath}");
        public void ShowProperties(string fullPath) => Calls.Add($"ShowProperties {fullPath}");
        public void RunCustomCommand(int commandIndex, string fullPath) => Calls.Add($"Custom {commandIndex} {fullPath}");
        public void RunCustomCommand(CustomCommandOptions command, string fullPath) => Calls.Add($"Custom {command.Label} {fullPath}");
    }
}
