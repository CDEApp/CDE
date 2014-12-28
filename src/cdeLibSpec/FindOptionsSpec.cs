using System;
using NSpec;
using NSubstitute.Experimental;
using cdeLib;
using NSubstitute;
using cdeLibTest;

namespace cdeLibSpec
{
    // ReSharper disable ImplicitlyCapturedClosure
    // ReSharper disable InconsistentNaming

    // these test might benefit from updates, as in break them into tests for two methods.
    // due to splitting GetFindPredicate() out of GetFindFunc().

    [Tag("describe_core_test")]
    public class find_options_spec : nspec
    {
        // System.Diagnostics.Debugger.Launch();

        static void log(string str, params object[] values)
        {
            Console.WriteLine(str, values);
        }

        public void given_default_findOptions()
        {
            FindOptions findOptions = null;
            before = () => {
                findOptions = new FindOptions();
            };

            specify = () => findOptions.LimitResultCount.should_be(10000);

            specify = () => findOptions.SkipCount.should_be(0);

            specify = () => findOptions.ProgressModifier.should_be(int.MaxValue);

            specify = () => findOptions.IncludeFiles.should_be_true();

            specify = () => findOptions.IncludeFolders.should_be_true();

            specify = () => findOptions.IncludePath.should_be_false();

            specify = () => findOptions.RegexMode.should_be_false();
        }

        public void given_FindOptions_with_matcherAll()
        {
            FindOptions findOptions = null;
            TraverseFunc visitorFunc = null;
            TraverseFunc findFunc = null;
            RootEntry rootEntry = null;
            DirEntry testFile = null;
            DirEntry testDir = null;

            before = () => {
                findOptions = new FindOptions();
                visitorFunc = Substitute.For<TraverseFunc>();
                var matcherAll = Substitute.For<Func<CommonEntry, DirEntry, bool>>();
                matcherAll(null, null).ReturnsForAnyArgs(true);

                findOptions.VisitorFunc = visitorFunc;
                findOptions.PatternMatcher = matcherAll;

                rootEntry = new RootEntry { FullPath = @"C:\" };
                testFile = new DirEntry(false) { Path = @"TestFile1" };
                testDir = new DirEntry(true) { Path = @"TestDir" };
            };



            describe["with test entry \"TestFile1\""] = () => {

                describe["when find items before limit"] = () => {
                    before = () => {
                        findFunc = findOptions.GetFindFunc(new[] {0}, new[] {int.MaxValue});
                    };

                    it["find calls found"] = () => {
                        findFunc(rootEntry, testFile);
                        //findOptions.VisitorFunc.Received().Invoke(rootEntry, testFile);
                        findOptions.VisitorFunc.Received()(rootEntry, testFile);
                    };

                    it["find returns true if found returns true"] = () => {
                        visitorFunc(null, null).ReturnsForAnyArgs(true);
                        findFunc(rootEntry, testFile).should_be_true();
                    };

                    it["find returns false if found returns false"] = () => {
                        // findprecicate is seperate from foundVisitor :(........ 
                        // this is borked.
                        visitorFunc(null, null).ReturnsForAnyArgs(false);
                        findFunc(rootEntry, testFile).should_be_false();
                    };
                };

                describe["when find items at limit"] = () => {
                    before = () => {
                        findFunc = findOptions.GetFindFunc(new[] {0}, new[] {1});
                    };

                    it["find calls found"] = () => {
                        findFunc(rootEntry, testFile);
                        findOptions.VisitorFunc.Received().Invoke(rootEntry, testFile);
                    };

                    it["find returns false if found returns true"] = () => {
                        visitorFunc(null, null).ReturnsForAnyArgs(true);
                        findFunc(rootEntry, testFile).should_be_false();
                    };
                };

                describe["given find excludes Files"] = () => {
                    before = () => {
                        findOptions.IncludeFiles = false;
                        findFunc = findOptions.GetFindFunc(new[] {0}, new[] {int.MaxValue});
                    };

                    it["find doesn't call found"] = () => {
                        findFunc(rootEntry, testFile);
                        findOptions.VisitorFunc.DidNotReceiveWithAnyArgs().Invoke(null, null);
                    };
                };

                describe["given find excludes Folders"] = () => {
                    before = () => {
                        findOptions.IncludeFolders = false;
                        findFunc = findOptions.GetFindFunc(new[] {0}, new[] {int.MaxValue});
                    };

                    it["find calls found"] = () => {
                        findFunc(rootEntry, testFile);
                        findOptions.VisitorFunc.ReceivedWithAnyArgs().Invoke(null, null);
                    };
                };
            };

            describe["with test entry \"TestDir1\""] = () => {

                describe["given find excludes Files"] = () => {
                    before = () => {
                        findOptions.IncludeFiles = false;
                        findFunc = findOptions.GetFindFunc(new[] {0}, new[] {int.MaxValue});
                    };

                    it["find calls found"] = () => {
                        findFunc(rootEntry, testDir);
                        findOptions.VisitorFunc.ReceivedWithAnyArgs().Invoke(null, null);
                    };
                };

                describe["given find excludes Folders"] = () => {
                    before = () => {
                        findOptions.IncludeFolders = false;
                        findFunc = findOptions.GetFindFunc(new[] {0}, new[] {int.MaxValue});
                    };

                    it["find doesn't call found"] = () => {
                        findFunc(rootEntry, testDir);
                        findOptions.VisitorFunc.DidNotReceiveWithAnyArgs().Invoke(null, null);
                    };
                };
            };
        }

        public void FindFunc_Spec_for_substring_matcher()
        {
            FindOptions findOptions = null;
            RootEntry rootEntry = null;
            DirEntry testFile = null;
            before = () =>
            {
                findOptions = new FindOptions();
                rootEntry = new RootEntry { FullPath = @"C:\" };
                testFile = new DirEntry(false) { Path = @"TestFile1" };
            };

            describe["with test entry \"TestFile1\""] = () => {

                describe["given that matcher excludes path"] = () => {

                    it["can't find \"x\""] = () => {
                        findOptions.Pattern = "x";
                        var matcher = findOptions.GetPatternMatcher();
                        matcher(rootEntry, testFile).should_be_false();
                    };

                    it["can find \"estFi\""] = () => {
                        findOptions.Pattern = "estFi";
                        var matcher = findOptions.GetPatternMatcher();
                        matcher(rootEntry, testFile).should_be_true();
                    };

                    it["can find \"testfile\" [note case variance]"] = () => {
                        findOptions.Pattern = "testfile";
                        var matcher = findOptions.GetPatternMatcher();
                        matcher(rootEntry, testFile).should_be_true();
                    };

                    it["can't find string \"C:\""] = () => {
                        findOptions.Pattern = @"C:";
                        var matcher = findOptions.GetPatternMatcher();
                        matcher(rootEntry, testFile).should_be_false();
                    };
                };

                describe["given that matcher includes path"] = () => {
                    before = () => {
                        findOptions.IncludePath = true;
                    };

                    it["can find string \"C:\" in name including path "] = () => {
                        findOptions.Pattern = @"C:";
                        var matcher = findOptions.GetPatternMatcher();
                        matcher(rootEntry, testFile).should_be_true();
                    };
                };
            };
        }

        // words seperate by white space are terms in search
        // each term must be found to return true - no overlap of terms.
        // terms may be matched in any order as long as they dont overlap [as long as they arnt adjacent]
        // * in a term means wildcard... so term is split to two terms that must match adjacent. [or fudge with regex]
        // * at start or end of term is redundant as terms sub string match within constraints produced by other terms.
        public void FindFunc_Spec_for_extended_matcher_zaza()
        {
            //    // new mode - not written yet.
            //    context["given test entry \"testfile1\""] = () => {
            //    };

            //    it["can find \"test\""] = () => {

            //    };
            //    it["can find \"test dir\""] = () => {

            //    };
            //    it["can find \"dir test\""] = () => {

            //    };
            //    it["can't find \"test tdir\" as overlap terms"] = () => {

            //    };
            //    it["can find \"te*ir\""] = () => {

            //    };
            //    it["can find \"test*\" this is in practice same as \"test\""] = () => {

            //    };
            //    it["can find \"t*t f*e\" on file"] = () => {

            //    };
        }

        public void FindFunc_Spec_for_limit()
        {

            describe["given test entry tree"] = () =>
            {
                // ReSharper disable TooWideLocalVariableScope
                DirEntry de2a;
                DirEntry de2b;
                DirEntry de2c;
                DirEntry de3a = null;
                DirEntry de4a = null;
                // ReSharper restore TooWideLocalVariableScope
                RootEntry re = null;
                FindOptions findOptions = null;
                Func<CommonEntry, DirEntry, bool> matcherAll = null;

                before = () =>
                {
                    // NOTE: test tree entry entry  is de2c,de2a,de2b,de3a,de4a
                    re = RootEntryTestBase.NewTestRootEntry(out de2a, out de2b, out de2c, out de3a, out de4a);
                    re.SetInMemoryFields();
                    matcherAll = Substitute.For<Func<CommonEntry, DirEntry, bool>>();
                    matcherAll(null, null).ReturnsForAnyArgs(true);
                    findOptions = new FindOptions();

                };

                describe["with FindOptions matching all entries"] = () =>
                {
                    TraverseFunc visitorFunc = null;

                    before = () =>
                    {
                        visitorFunc = Substitute.For<TraverseFunc>();
                        visitorFunc(null, null).ReturnsForAnyArgs(x => true);
                        findOptions.Pattern = string.Empty;
                        findOptions.PatternMatcher = matcherAll;
                        findOptions.VisitorFunc = visitorFunc;
                    };

                    describe["given find limit 2"] = () =>
                    {
                        before = () =>
                        {
                            findOptions.LimitResultCount = 2;
                            findOptions.Find(new[] { re });
                        };

                        specify = () => visitorFunc.ReceivedWithAnyArgs(2).Invoke(null, null);

                        specify = () => findOptions.ProgressCount.should_be(2);
                    };

                    describe["given find limit 4"] = () =>
                    {
                        before = () =>
                        {
                            findOptions.LimitResultCount = 4;
                            findOptions.Find(new[] { re });
                        };

                        specify = () => visitorFunc.ReceivedWithAnyArgs(4).Invoke(null, null);

                        specify = () => findOptions.ProgressCount.should_be(4);
                    };

                    describe["given large limit"] = () =>
                    {
                        before = () =>
                        {
                            findOptions.LimitResultCount = int.MaxValue;
                        };

                        describe["will find all"] = () =>
                        {
                            before = () => findOptions.Find(new[] { re });

                            specify = () => visitorFunc.ReceivedWithAnyArgs(5).Invoke(null, null);

                            specify = () => findOptions.ProgressCount.should_be(5);
                        };

                        describe["and given SkipCount 5"] = () =>
                        {
                            before = () =>
                            {
                                findOptions.SkipCount = 5;
                                findOptions.Find(new[] { re });
                            };

                            specify = () => visitorFunc.ReceivedWithAnyArgs(0).Invoke(null, null);

                            specify = () => findOptions.ProgressCount.should_be(5);
                        };

                        describe["and given SkipCount 6"] = () =>
                        {
                            before = () =>
                            {
                                findOptions.SkipCount = 6;
                                findOptions.Find(new[] { re });
                            };

                            specify = () => visitorFunc.ReceivedWithAnyArgs(0).Invoke(null, null);

                            specify = () => findOptions.ProgressCount.should_be(5);
                        };

                    };

                    describe["When find limit 2, pattern 'a'"] = () =>
                    {
                        before = () =>
                        {
                            findOptions.Pattern = "a";
                            findOptions.LimitResultCount = 2;
                            findOptions.Find(new[] { re });
                        };

                        specify = () => visitorFunc.ReceivedWithAnyArgs(2).Invoke(null, null);

                        specify = () => findOptions.ProgressCount.should_be(4);
                    };

                    describe["When find limit 2, pattern 'a' with Skip count 2"] = () =>
                    {
                        before = () =>
                        {
                            findOptions.LimitResultCount = 2;
                            findOptions.SkipCount = 2;
                            findOptions.Pattern = "a";
                            findOptions.Find(new[] { re });
                        };

                        specify = () => visitorFunc.ReceivedWithAnyArgs(2).Invoke(null, null);

                        specify = () => findOptions.ProgressCount.Is(5);

                        it["found received in expected order"] =
                            () => Received.InOrder(() =>
                            {
                                visitorFunc.Received().Invoke(Arg.Any<CommonEntry>(), de3a);
                                visitorFunc.Received().Invoke(Arg.Any<CommonEntry>(), de4a);
                            });
                    };
                };
            };
            // TODO - capture SkipCount and interface and pass it back later ? [ProgressCount from findOptions]
            //        maybe this involves holding onto FindOptions on serverside in Web ?
            // TODO - if SkipCount >= Limit then dont do any work return empty.
        }
    }

    // ReSharper restore InconsistentNaming
    // ReSharper restore ImplicitlyCapturedClosure
}
