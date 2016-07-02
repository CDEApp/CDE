using System.Linq;
using NSpec;
using NSpec.Domain;
using NSpec.Domain.Formatters;
using NUnit.Framework;

/*
 * Howdy,
 * 
 * This is NSpec's DebuggerShim.  It will allow you to use TestDriven.Net or Resharper's test runner to run
 * NSpec tests that are in the same Assembly as this class.  
 * 
 * It's DEFINITELY worth trying specwatchr (http://nspec.org/continuoustesting). Specwatchr automatically
 * runs tests for you.
 * 
 * If you ever want to debug a test when using Specwatchr, simply put the following line in your test:
 * 
 *     System.Diagnostics.Debugger.Launch()
 *     
 * Visual Studio will detect this and will give you a window which you can use to attach a debugger.
 */

// Shim modified as follows.
// Reference https://chrisseroka.wordpress.com/2014/11/27/debugging-nspec-with-visualstudioresharper-simple-trick/
[TestFixture]
public abstract class nspec : global::NSpec.nspec
{
    // Alternate 
    [Test]
    public void NspecNunitAdapter()
    {
        var currentSpec = this.GetType();
        var finder = new SpecFinder(new[] { currentSpec });
        var tagsFilter = new Tags().Parse(currentSpec.Name);
        var builder = new ContextBuilder(finder, tagsFilter, new DefaultConventions());
        var runner = new ContextRunner(tagsFilter, new ConsoleFormatter(), false);
        var results = runner.Run(builder.Contexts().Build());

        //assert that there aren't any failures
        results.Failures().Count().should_be(0);
    }
}