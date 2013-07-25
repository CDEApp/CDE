using System.Linq;
using NSpec;
using NSpec.Domain;
using NSpec.Domain.Formatters;
using NUnit.Framework;

namespace cdeLibSpec
{
    // ReSharper disable InconsistentNaming
    /// <summary>
    /// Enable running NSpec tests in NUnit test runner for convenience.
    /// To call debugger use System.Diagnostics.Debugger.Launch();
    /// </summary>
    public class nspec_as_nunit : nspec
    {
        [Test]
        public void Execute()
        {
            // from https://github.com/mattflo/NSpec/blob/master/NSpecSpecs/DebuggerShim.cs
            const string tagOrClassName = "describe_test";

            var types = GetType().Assembly.GetTypes();
            // OR
            // var types = new Type[]{typeof(Some_Type_Containg_some_Specs)};
            // ReSharper disable RedundantArgumentDefaultValue
            var finder = new SpecFinder(types, "");
            // ReSharper restore RedundantArgumentDefaultValue
            var builder = new ContextBuilder(finder, new Tags().Parse(tagOrClassName), new DefaultConventions());
            //var builder = new ContextBuilder(finder, new DefaultConventions());
            var runner = new ContextRunner(builder, new ConsoleFormatter(), false);
            var results = runner.Run(builder.Contexts().Build());

            //assert that there aren't any failures
            results.Failures().Count().should_be(0);
        }
    }
    // ReSharper restore InconsistentNaming
}