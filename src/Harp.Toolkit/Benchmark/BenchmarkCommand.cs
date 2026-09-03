using System.CommandLine;
using Spectre.Console;
using Harp.Generators;
using Harp.Toolkit.Benchmark;
using Harp.Toolkit.Benchmark.Suites;
using Harp.Toolkit.Generate;

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

        Option<string?> clockPortOption = new("--clock-port")
        {
            Description = "Serial port of the reference clock device. Enables clock alignment tests.",
            Required = false,
        };

        Option<int?> regClockOption = new("--pps-address")
        {
            Description = "Register address on the tested device (--port) that emits an event whenever the incoming PPS signal goes high. Enables PPS alignment test.",
            Required = false,
        };

        Option<int> clockSamplesOption = new("--clock-samples")
        {
            Description = "Number of PPS event pairs to collect for the PPS alignment test. Default: 5.",
            Required = false,
        };
        clockSamplesOption.DefaultValueFactory = _ => 5;

        Option<FileInfo> deviceYmlOption = new("--device-yml")
        {
            Description = "Path to the device's device.yml. Enables validation of the generated C# interface against a live read of every declared register, and cross-checks WhoAmI/firmware/hardware versions.",
            Required = false,
        };
        OptionValidation.AcceptExistingOnly(deviceYmlOption);

        Options.Add(portNameOption);
        Options.Add(fileOption);
        Options.Add(verboseOption);
        Options.Add(clockPortOption);
        Options.Add(regClockOption);
        Options.Add(clockSamplesOption);
        Options.Add(deviceYmlOption);
        SetAction(parsedResult =>
        {
            string portName = parsedResult.GetRequiredValue(portNameOption);
            FileInfo? reportFile = parsedResult.GetValue(fileOption);
            bool verbose = parsedResult.GetValue(verboseOption);
            string? clockPort = parsedResult.GetValue(clockPortOption);
            ClockTestOptions? clockOptions = clockPort is null ? null : new ClockTestOptions(
                ClockPort: clockPort,
                PpsAddress: parsedResult.GetValue(regClockOption),
                ClockSamples: parsedResult.GetValue(clockSamplesOption));
            FileInfo? deviceYml = parsedResult.GetValue(deviceYmlOption);
            return RunBenchmarks(portName, reportFile, verbose, clockOptions, deviceYml, CancellationToken.None);
        });
    }

    static async Task RunBenchmarks(string portName, FileInfo? reportFile, bool verbose, ClockTestOptions? clockOptions, FileInfo? deviceYml, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine($"Running tests on [bold]{portName}[/]...");
        if (clockOptions is not null)
            AnsiConsole.MarkupLine($"Clock reference device: [bold]{clockOptions.ClockPort}[/]");

        DeviceMetadata? deviceMetadata = null;
        string? deviceRawYaml = null;
        if (deviceYml is not null)
        {
            AnsiConsole.Markup($"Loading device metadata from [bold]{deviceYml.FullName}[/]...");
            deviceMetadata = GeneratorHelper.ReadDeviceMetadata(deviceYml.FullName);
            deviceRawYaml = await File.ReadAllTextAsync(deviceYml.FullName, cancellationToken);
            AnsiConsole.MarkupLine($" [green]Done![/] ({deviceMetadata.Registers.Count} registers)");
        }

        var runner = new CoreRunner(clockOptions, deviceMetadata, deviceRawYaml);
        var report = new Report
        {
            DeviceName = $"Harp Device ({portName})",
            RunDate = DateTime.Now
        };

        int currentTest = 0;
        await foreach (var (suite, result) in runner.RunAllAsync(portName, cancellationToken, (suite, testName, testDesc) =>
        {
            // Print "Running" status before test execution (without newline)
            currentTest++;
            Console.Write($"({currentTest}/{runner.TestCount}) {suite.GetType().Name}::{testName} .... Running...");
        }))
        {
            // Clear the line by moving cursor to start and overwriting with spaces, then print result
            Console.Write($"\r{new string(' ', Console.WindowWidth - 1)}\r");
            AnsiConsole.MarkupLine($"[grey]({currentTest}/{runner.TestCount}) {suite.GetType().Name}::{result.Name}[/] .... {GetResultMarkup(result.Result)}");

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
                        new Markup($"[bold]{Markup.Escape(test.Name)}[/]\n[dim]{Markup.Escape(test.Description)}[/]"),
                        new Markup(GetResultMarkup(test.Result)),
                        new Markup(Markup.Escape(details)),
                        new Markup(Markup.Escape(message))
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
        public CoreRunner(
            ClockTestOptions? clockOptions = null,
            DeviceMetadata? deviceMetadata = null,
            string? deviceRawYaml = null) : base()
        {
            AddSuite(new R_WHO_AM_I());
            AddSuite(new R_HW_VERSION_H());
            AddSuite(new R_HW_VERSION_L());
            AddSuite(new R_ASSEMBLY_VERSION());
            AddSuite(new R_CORE_VERSION_H());
            AddSuite(new R_CORE_VERSION_L());
            AddSuite(new R_FW_VERSION_H());
            AddSuite(new R_FW_VERSION_L());
            AddSuite(new R_TIMESTAMP_SECOND());
            AddSuite(new R_TIMESTAMP_MICRO());
            AddSuite(new R_OPERATION_CTRL());
            AddSuite(new R_RESET_DEV());
            AddSuite(new R_DEVICE_NAME());
            AddSuite(new R_SERIAL_NUMBER());
            AddSuite(new R_CLOCK_CONFIG());
            AddSuite(new R_TIMESTAMP_OFFSET());
            AddSuite(new R_UID());
            AddSuite(new R_TAG());
            AddSuite(new R_HEARTBEAT());
            AddSuite(new R_VERSION());
            AddSuite(new RoundTripTestSuite());
            AddSuite(new ClockTestSuite(clockOptions));
            AddSuite(new DeviceInterfaceSuite(deviceMetadata, deviceRawYaml));
        }
    }
}


