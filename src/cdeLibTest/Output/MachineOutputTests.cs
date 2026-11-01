using cde;
using NUnit.Framework;

namespace cdeLibTest.Output;

/// <summary>Which command lines ask for a machine format, so standard output is redirected before anything is written (#173).</summary>
[TestFixture]
public class MachineOutputTests
{
    [TestCase("find", "x", "--json")]
    [TestCase("find", "x", "--csv")]
    [TestCase("findpath", "x", "--json")]
    [TestCase("grep", "x", "--csv")]
    [TestCase("greppath", "x", "--json")]
    [TestCase("everything", "x", "--csv")]
    [TestCase("dupes", "--json")]
    [TestCase("hash", "--csv")]
    [TestCase("HASH", "--CSV")]
    public void A_flag_on_a_verb_that_supports_machine_output_is_requested(params string[] args)
    {
        Assert.That(MachineOutput.IsRequested(args), Is.True);
    }

    [TestCase("find", "--", "--csv")]
    [TestCase("find", "x", "--", "--json")]
    [TestCase("--", "--json")]
    public void Nothing_after_the_double_dash_terminator_counts(params string[] args)
    {
        Assert.That(MachineOutput.IsRequested(args), Is.False);
    }

    [TestCase("scan", "--exclude", "--json")]
    [TestCase("scan", "c:\\", "--csv")]
    [TestCase("migrate", "--json")]
    [TestCase("repl", "--csv")]
    public void A_verb_without_machine_output_never_requests_it(params string[] args)
    {
        Assert.That(MachineOutput.IsRequested(args), Is.False);
    }

    [Test]
    public void No_arguments_request_nothing()
    {
        Assert.That(MachineOutput.IsRequested([]), Is.False);
    }
}
