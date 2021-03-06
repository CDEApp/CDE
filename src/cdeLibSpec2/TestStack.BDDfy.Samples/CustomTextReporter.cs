﻿using System;
using System.IO;
using System.Linq;
using System.Text;
using TestStack.BDDfy;

namespace cdeLibSpec2.TestStack.BDDfy.Samples
{
    /// <summary>
    /// This is a custom reporter that shows you how easily you can create a custom report.
    /// Just implemented IProcessor and you are done
    /// </summary>
    public class CustomTextReporter : IProcessor
    {
        private static readonly string Path;

        private static string OutputDirectory
        {
            get
            {
                var codeBase = typeof(CustomTextReporter).Assembly.CodeBase;
                var uri = new UriBuilder(codeBase);
                var path = Uri.UnescapeDataString(uri.Path);
                return System.IO.Path.GetDirectoryName(path);
            }
        }

        static CustomTextReporter()
        {
            Path = System.IO.Path.Combine(OutputDirectory, "BDDfy-text-report.txt");
            
            if(File.Exists(Path))
                File.Delete(Path);

            var header = 
                " A custom report created from your test assembly with no required configuration " + 
                Environment.NewLine + 
                Environment.NewLine + 
                Environment.NewLine + 
                Environment.NewLine;
            File.AppendAllText(Path, header);
        }

        public void Process(Story story)
        {
            // use this report only for tic tac toe stories
            if (story.Metadata == null || !story.Metadata.Type.Name.Contains("TicTacToe"))
                return;

            var scenario = story.Scenarios.First();
            var scenarioReport = new StringBuilder();
            scenarioReport.AppendLine($" SCENARIO: {scenario.Title}  ");

            if (scenario.Result != Result.Passed && scenario.Steps.Any(s => s.Exception != null))
            {
                scenarioReport.Append($"    {scenario.Result} : ");
                scenarioReport.AppendLine(scenario.Steps.First(s => s.Result == scenario.Result).Exception.Message);
            }

            scenarioReport.AppendLine();

            foreach (var step in scenario.Steps)
                scenarioReport.AppendLine(string.Format("   [{1}] {0}", step.Title, step.Result));

            scenarioReport.AppendLine("--------------------------------------------------------------------------------");
            scenarioReport.AppendLine();

            File.AppendAllText(Path, scenarioReport.ToString());
        }

        public ProcessType ProcessType
        {
            get { return ProcessType.Report; }
        }
    }
}