using cdeWin;
using NUnit.Framework;

namespace cdeWinTest;

/// <summary>
/// The Files/Folders combo in Everything mode: it shows "Files and Folders" (index 0) and is greyed out, and the
/// user's saved choice comes back (and is what config records) when Everything is switched off.
/// </summary>
[TestFixture]
public class FindEntryFilterStateTests
{
    private const int FilesOnly = 1;
    private const int FoldersOnly = 2;

    [Test]
    public void Plain_search_shows_and_enables_the_chosen_entry_filter()
    {
        var state = new FindEntryFilterState();
        state.Choose(FilesOnly);

        Assert.That(state.Shown, Is.EqualTo(FilesOnly));
        Assert.That(state.ComboEnabled, Is.True);
        Assert.That(state.Saved, Is.EqualTo(FilesOnly));
    }

    [Test]
    public void Everything_mode_shows_files_and_folders_greyed_out_and_keeps_the_saved_choice()
    {
        var state = new FindEntryFilterState();
        state.Choose(FoldersOnly);

        state.SetEverything(true);

        Assert.That(state.Shown, Is.EqualTo(0));
        Assert.That(state.ComboEnabled, Is.False);
        Assert.That(state.Saved, Is.EqualTo(FoldersOnly), "config records the saved choice, not the displayed one");
    }

    [Test]
    public void Switching_everything_off_gives_the_saved_choice_back()
    {
        var state = new FindEntryFilterState();
        state.Choose(FilesOnly);
        state.SetEverything(true);

        state.SetEverything(false);

        Assert.That(state.Shown, Is.EqualTo(FilesOnly));
        Assert.That(state.ComboEnabled, Is.True);
    }

    [Test]
    public void Restoring_config_in_everything_mode_stores_the_choice_without_showing_it()
    {
        var state = new FindEntryFilterState();
        state.SetEverything(true); // config restore sets Everything first

        state.Choose(FoldersOnly);

        Assert.That(state.Shown, Is.EqualTo(0));
        Assert.That(state.Saved, Is.EqualTo(FoldersOnly));
        state.SetEverything(false);
        Assert.That(state.Shown, Is.EqualTo(FoldersOnly));
    }

    [Test]
    public void Default_is_files_and_folders()
    {
        var state = new FindEntryFilterState();

        Assert.That(state.Shown, Is.EqualTo(0));
        Assert.That(state.Saved, Is.EqualTo(0));
        Assert.That(state.ComboEnabled, Is.True);
    }
}
