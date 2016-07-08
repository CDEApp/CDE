using cdeLibSpec2.TestStack.BDDfy.Samples.Atm;
using TestStack.BDDfy.Configuration;
using TestStack.BDDfy.Reporters.Html;

namespace cdeLibSpec2.TestStack.BDDfy.Samples
{
    /// <summary>
    /// Used by the ModuleInit. All code inside the Initialize method is ran as soon as the assembly is loaded.
    /// </summary>
    public static class ModuleInitializer
    {
        /// <summary>
        /// Initializes the module.
        /// </summary>
        public static void Initialize()
        {
            //Configurator.Processors.TestRunner.Disable();
            //Configurator.Processors.Add(() => new CustomTextReporter());
            Configurator.BatchProcessors.MarkDownReport.Enable();
            //Configurator.BatchProcessors.DiagnosticsReport.Enable();
            //Configurator.BatchProcessors.Add(new HtmlReporter(new AtmHtmlReportConfig(), new MetroReportBuilder()));
        }
    }
}
