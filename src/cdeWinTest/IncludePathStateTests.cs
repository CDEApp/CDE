using cdeWin;
using NUnit.Framework;

namespace cdeWinTest;

/// <summary>
/// The Include Path combo in Everything mode: it shows "Exclude Path from Search" and is greyed out, because a
/// path: term in the query reaches the path. The saved choice comes back (and is what config records) when
/// Everything is switched off.
/// </summary>
[TestFixture]
public class IncludePathStateTests
{
    [Test]
    public void Default_is_include_path_and_enabled()
    {
        var state = new IncludePathState();

        Assert.That(state.Saved, Is.True);
        Assert.That(state.Shown, Is.True);
        Assert.That(state.ComboEnabled, Is.True);
    }

    [Test]
    public void Everything_mode_shows_exclude_greyed_out_and_keeps_the_saved_choice()
    {
        var state = new IncludePathState();
        state.Choose(true);

        state.SetEverything(true);

        Assert.That(state.Shown, Is.False);
        Assert.That(state.ComboEnabled, Is.False);
        Assert.That(state.Saved, Is.True, "config records the saved choice, not the displayed one");
    }

    [Test]
    public void Switching_everything_off_gives_the_saved_choice_back()
    {
        var state = new IncludePathState();
        state.Choose(true);
        state.SetEverything(true);

        state.SetEverything(false);

        Assert.That(state.Shown, Is.True);
        Assert.That(state.ComboEnabled, Is.True);
    }

    [Test]
    public void Restoring_config_in_everything_mode_stores_the_choice_without_showing_it()
    {
        var state = new IncludePathState();
        state.SetEverything(true); // config restore sets Everything first

        state.Choose(true);

        Assert.That(state.Shown, Is.False);
        Assert.That(state.Saved, Is.True);
        state.SetEverything(false);
        Assert.That(state.Shown, Is.True);
    }
}
