using System.Linq;
using cde.CommandLine;
using CommandLine;
using NUnit.Framework;

namespace cdeLibTest;

/// <summary>
/// The <c>cde everything</c> command line: one required query, the shared --template and --filter options, and no
/// regex, path, files or folders options, because the query text decides all of that.
/// </summary>
[TestFixture]
public class EverythingCommandTests
{
    private static ParserResult<object> Parse(params string[] args) =>
        CommandLineParserBuilder.Build().ParseArguments<EverythingOptions, cde.CommandLine.FindOptions>(args);

    private static EverythingOptions Parsed(params string[] args)
    {
        var result = Parse(args);
        Assert.That(result, Is.InstanceOf<Parsed<object>>(), string.Join(" ", args));
        return (EverythingOptions)((Parsed<object>)result).Value;
    }

    [Test]
    public void The_query_is_the_one_positional_value()
    {
        var options = Parsed("everything", "do win");

        Assert.That(options.Value, Is.EqualTo("do win"));
        Assert.That(options.Template, Is.Null);
        Assert.That(options.Filter ?? [], Is.Empty);
    }

    [Test]
    public void Template_and_repeated_filters_are_accepted()
    {
        var options = Parsed("everything", "file: .mp3", "--template", "{filename}", "--filter", "{size:>1KB}",
            "--filter", "{size:<4MB}");

        Assert.That(options.Value, Is.EqualTo("file: .mp3"));
        Assert.That(options.Template, Is.EqualTo("{filename}"));
        Assert.That(options.Filter.ToArray(), Is.EqualTo(new[] { "{size:>1KB}", "{size:<4MB}" }));
    }

    [Test]
    public void Operator_characters_in_a_quoted_query_are_kept_as_is()
    {
        Assert.That(Parsed("everything", "<a | b> !tmp").Value, Is.EqualTo("<a | b> !tmp"));
        Assert.That(Parsed("everything", "path:\"a b\" folder:").Value, Is.EqualTo("path:\"a b\" folder:"));
    }

    [Test]
    public void The_query_is_required()
    {
        Assert.That(Parse("everything"), Is.InstanceOf<NotParsed<object>>());
    }

    [TestCase("--regex")]
    [TestCase("--path")]
    [TestCase("--files")]
    [TestCase("--folders")]
    [TestCase("--everything")]
    [TestCase("-e")]
    public void There_are_no_other_options(string option)
    {
        Assert.That(Parse("everything", "a", option), Is.InstanceOf<NotParsed<object>>());
    }

    [Test]
    public void Find_does_not_get_an_everything_flag()
    {
        Assert.That(Parse("find", "a", "--everything"), Is.InstanceOf<NotParsed<object>>());
    }
}
