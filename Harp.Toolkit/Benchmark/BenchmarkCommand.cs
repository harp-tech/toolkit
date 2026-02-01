using System.CommandLine;
using Spectre.Console;

namespace Harp.Toolkit;
public class BenchmarkCommand : Command
{
    public BenchmarkCommand()
        : base("benchmark", "Run benchmark tests on the device.")
    {
        PortNameOption portNameOption = new();
        Option<FileInfo?> fileOption = new("--report")
        {
            Description = "Path to the HTML report generated after running tests.",
            Required = false,
        };

        Option<bool> verboseOption = new("--verbose")
        {
            Description = "Show detailed results for each test.",
            Required = false,
        };
        Options.Add(portNameOption);
        Options.Add(fileOption);
        Options.Add(verboseOption);
        SetAction(parsedResult =>
        {
            string portName = parsedResult.GetRequiredValue(portNameOption);
            FileInfo? reportFile = parsedResult.GetValue(fileOption);
            bool verbose = parsedResult.GetValue(verboseOption);
            return RunBenchmarks(portName, reportFile, verbose, CancellationToken.None);
        });
    }

    static async Task RunBenchmarks(string portName, FileInfo? reportFile, bool verbose, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine($"Running tests on [bold]{portName}[/]...");

        var runner = new CoreRunner();
        var report = new Report
        {
            DeviceName = $"Harp Device ({portName})",
            RunDate = DateTime.Now
        };

        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Running tests...[/]", true, runner.TestCount);

                await foreach (var (suite, result) in runner.RunAllAsync(portName, cancellationToken))
                {
                    task.Increment(1);
                    AnsiConsole.MarkupLine($"[grey]{suite.GetType().Name}::{result.Name}[/] .... {GetResultMarkup(result.Result)}");

                    var suiteResult = report.Suites.FirstOrDefault(s => s.Name == suite.GetType().Name);
                    if (suiteResult == null)
                    {
                        suiteResult = new SuiteResult
                        {
                            Name = suite.GetType().Name,
                            Description = suite.Description
                        };
                        report.Suites.Add(suiteResult);
                    }
                    suiteResult.Results.Add(result);
                }
            });

        if (verbose)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule("[yellow]Detailed Results[/]"));
            foreach (var suite in report.Suites)
            {
                AnsiConsole.MarkupLine($"[bold underline]{suite.Name}[/]");
                AnsiConsole.MarkupLine($"[dim]{suite.Description}[/]");

                var table = new Table();
                table.AddColumn("Test Case");
                table.AddColumn("Status");
                table.AddColumn("Details");
                table.AddColumn("Message");

                foreach (var test in suite.Results)
                {
                    string details = "";
                    string message = test.Result.Message ?? "";

                    if (test.Result is NumericBenchmarkResult bsr)
                    {
                        details = $"Mean: {bsr.Summary.Mean:F4}\nMedian: {bsr.Summary.Median:F4}\nStdDev: {bsr.Summary.StdDev:F4}\nMin: {bsr.Summary.Min:F4}\nMax: {bsr.Summary.Max:F4}\nPercentiles: 99th={bsr.Summary.Percentile99:F4}, 01th={bsr.Summary.Percentile01:F4}";
                    }
                    else if (test.Result is ErrorResult er)
                    {
                        details = $"{er.Exception.GetType().Name}";
                    }
                    else
                    {
                        var valProp = test.Result?.GetType().GetProperty("Value");
                        if (valProp != null)
                        {
                            var val = valProp.GetValue(test.Result);
                            details = val?.ToString() ?? "";
                        }
                    }

                    table.AddRow(
                        new Markup($"[bold]{test.Name}[/]\n[dim]{test.Description}[/]"),
                        new Markup(GetResultMarkup(test.Result)),
                        new Markup(details),
                        new Markup(message)
                    );
                }
                AnsiConsole.Write(table);
                AnsiConsole.WriteLine();
            }
        }

        if (reportFile != null)
        {
            AnsiConsole.Markup("Generating HTML report...");
            string html = await HtmlReportGenerator.GenerateAsync(report);
            string fileName = reportFile?.FullName ?? $"TestReport_{DateTime.Now:yyyyMMdd_HHmmss}.html";
            await File.WriteAllTextAsync(fileName, html, cancellationToken);
            AnsiConsole.MarkupLine($"[green]Done![/] Report generated: [link]{fileName}[/]");
        }
    }

    static string GetResultMarkup(IResult result)
    {
        return result.Status switch
        {
            Status.Passed => "[green]Passed[/]",
            Status.Failed => "[red]Failed[/]",
            Status.Error => "[red]Error[/]",
            Status.Skipped => "[yellow]Skipped[/]",
            _ => $"[white]{result.Status}[/]"
        };
    }

    class CoreRunner : Runner
    {
        public CoreRunner() : base()
        {
            AddSuite(new WhoAmISuite());
            AddSuite(new RoundTripTestSuite());
            AddSuite(new TimestampSecondsSuite());
        }
    }
}


