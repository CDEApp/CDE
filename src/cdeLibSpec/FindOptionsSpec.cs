using System;
using NSpec;
using cdeLib;
using NSubstitute;

namespace cdeLibSpec
{
    // ReSharper disable ImplicitlyCapturedClosure
    // ReSharper disable InconsistentNaming

    [Tag("describe_test")]
    public class FindOptions_Spec : nspec
    {
        // System.Diagnostics.Debugger.Launch();

        static void log(string str, params object[] values)
        {
            Console.WriteLine(str, values);
        }

        public void given_new_FindOptions()
        {
            FindOptions findOptions = null;
            before = () => {
                findOptions = new FindOptions();
            };

            it["default LimitResult is 10000"] =
                () => findOptions.LimitResultCount.should_be(10000);

            it["default ProgressModifier is int.MaxValue"] =
                () => findOptions.ProgressModifier.should_be(int.MaxValue);

            it["default IncludeFiles is true"] =
                () => findOptions.IncludeFiles.should_be_true();

            it["default IncludeFolders is true"] =
                () => findOptions.IncludeFolders.should_be_true();

            it["default IncludePath is false"] =
                () => findOptions.IncludePath.should_be_false();

            it["default RegexMode is false"] =
                () => findOptions.RegexMode.should_be_false();
        }

        public void given_FindOptions_with_matcherAll()
        {
            FindOptions findOptions = null;
            TraverseFunc foundFunc = null;
            TraverseFunc findFunc = null;
            RootEntry rootEntry = null;
            DirEntry testFile = null;
            DirEntry testDir = null;

            before = () => {
                findOptions = new FindOptions();
                foundFunc = Substitute.For<TraverseFunc>();
                var matcherAll = Substitute.For<Func<CommonEntry, DirEntry, bool>>();
                matcherAll(null, null).ReturnsForAnyArgs(true);

                findOptions.FoundFunc = foundFunc;
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
                        findOptions.FoundFunc.Received().Invoke(rootEntry, testFile);
                    };

                    it["find returns true if found returns true"] = () => {
                        foundFunc(null, null).ReturnsForAnyArgs(true);
                        findFunc(rootEntry, testFile).should_be_true();
                    };

                    it["find returns false if found returns false"] = () => {
                        foundFunc(null, null).ReturnsForAnyArgs(false);
                        findFunc(rootEntry, testFile).should_be_false();
                    };
                };

                describe["when find items at limit"] = () => {
                    before = () => {
                        findFunc = findOptions.GetFindFunc(new[] {0}, new[] {1});
                    };

                    it["find calls found"] = () => {
                        findFunc(rootEntry, testFile);
                        findOptions.FoundFunc.Received().Invoke(rootEntry, testFile);
                    };

                    it["find returns false if found returns true"] = () => {
                        foundFunc(null, null).ReturnsForAnyArgs(true);
                        findFunc(rootEntry, testFile).should_be_false();
                    };
                };

                context["given find excludes Files"] = () => {
                    before = () => {
                        findOptions.IncludeFiles = false;
                        findFunc = findOptions.GetFindFunc(new[] {0}, new[] {int.MaxValue});
                    };

                    it["find doesn't call found"] = () => {
                        findFunc(rootEntry, testFile);
                        findOptions.FoundFunc.DidNotReceiveWithAnyArgs().Invoke(null, null);
                    };
                };

                context["given find excludes Folders"] = () => {
                    before = () => {
                        findOptions.IncludeFolders = false;
                        findFunc = findOptions.GetFindFunc(new[] {0}, new[] {int.MaxValue});
                    };

                    it["find calls found"] = () => {
                        findFunc(rootEntry, testFile);
                        findOptions.FoundFunc.ReceivedWithAnyArgs().Invoke(null, null);
                    };
                };
            };

            describe["with test entry \"TestDir1\""] = () => {

                context["given find excludes Files"] = () => {
                    before = () => {
                        findOptions.IncludeFiles = false;
                        findFunc = findOptions.GetFindFunc(new[] {0}, new[] {int.MaxValue});
                    };

                    it["find calls found"] = () => {
                        findFunc(rootEntry, testDir);
                        findOptions.FoundFunc.ReceivedWithAnyArgs().Invoke(null, null);
                    };
                };

                context["given find excludes Folders"] = () => {
                    before = () => {
                        findOptions.IncludeFolders = false;
                        findFunc = findOptions.GetFindFunc(new[] {0}, new[] {int.MaxValue});
                    };

                    it["find doesn't call found"] = () => {
                        findFunc(rootEntry, testDir);
                        findOptions.FoundFunc.DidNotReceiveWithAnyArgs().Invoke(null, null);
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

                context["given that matcher excludes path"] = () => {

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

                context["given that matcher includes path"] = () => {
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
        public void FindFunc_Spec_for_extended_matcher()
        {
    
        }
    }
            //context["when extended matching"] = () => {
            //    // words seperate by white space are terms in search
            //    // each term must be found to return true - no overlap of terms
            //    // * in a term means wildcard..
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
            //};

    // ReSharper restore InconsistentNaming
    // ReSharper restore ImplicitlyCapturedClosure
}
