using cdeLib;
using Shouldly;
// ReSharper disable UnusedMember.Local
// ReSharper disable ArrangeTypeMemberModifiers

namespace cdeLibSpec2
{
    // ReSharper disable once InconsistentNaming
    public class Check_default_FindOptions_values
    {
        private FindOptions _findOptions;

        void GivenANewFindOptions()
        {
            _findOptions = new FindOptions();
        }

        // when does not apply here i believe.
        //void WhenICheckLimitResultCount() { }

        void ThenLimitResultCountShouldBe10000()
        {
            _findOptions.LimitResultCount.ShouldBe(10000);
        }

        void AndThenSkipCountShouldBe0()
        {
            _findOptions.SkipCount.ShouldBe(0);
        }

        void AndThenProgressModifierShouldBeMaxIntValue()
        {
            _findOptions.ProgressModifier.ShouldBe(int.MaxValue);
        }

        void AndThenIncludeFilesShouldBeMaxIntValue()
        {
            _findOptions.IncludeFiles.ShouldBeTrue();
        }

        void AndThenIncludeFoldersShouldBeTrue()
        {
            _findOptions.IncludeFolders.ShouldBeTrue();
        }

        void AndThenIncludePathShouldBeTrue()
        {
            _findOptions.IncludeFolders.ShouldBeTrue();
        }

        void AndThenRegexModeShouldBeFalse()
        {
            _findOptions.RegexMode.ShouldBeFalse();
        }
    }
}