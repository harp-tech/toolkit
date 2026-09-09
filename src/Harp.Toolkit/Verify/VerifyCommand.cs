using System.CommandLine;
using Spectre.Console;
using Harp.Generators;
using Harp.Toolkit.Verify.Suites;
using Harp.Toolkit.Generate;

namespace Harp.Toolkit.Verify;
public class VerifyCommand : Command
{
    public VerifyCommand()
        : base("verify", "Verify device conformance against the Harp specification.")
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

        Option<bool> prereleaseOption = new("--prerelease")
        {
            Description = "Include checks against specification text outside the stable baseline.",
            Required = false,
        };

        Option<string?> clockPortOption = new("--clock-port")
        {
            Description = "Serial port of the reference clock device. Enables clock alignment tests.",
            Required = false,
        };

        Option<int?> ppsEventOption = new("--pps-event")
        {
            Description = "Address of the register on the tested device (--port) that reports the incoming PPS pulse from the reference clock device. Enables the PPS alignment test, which also requires --clock-port.",
            Required = false,
        };

        Option<int> clockSamplesOption = new("--clock-samples")
        {
            Description = "Number of PPS event pairs to collect for the PPS alignment test.",
            Required = false,
        };
        clockSamplesOption.DefaultValueFactory = _ => 5;
        clockSamplesOption.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<int>() < 1)
                result.AddError("The number of clock samples must be greater than zero.");
        });

        Option<FileInfo> metadataOption = new("--metadata")
        {
            Description = "The path to the file describing the device registers. Enables validation of the generated interface against a live read of every declared register, and cross-checks the WhoAmI, firmware and hardware versions.",
            Required = false,
        };
        OptionValidation.AcceptExistingOnly(metadataOption);

        Options.Add(portNameOption);
        Options.Add(fileOption);
        Options.Add(verboseOption);
        Options.Add(prereleaseOption);
        Options.Add(clockPortOption);
        Options.Add(ppsEventOption);
        Options.Add(clockSamplesOption);
        Options.Add(metadataOption);
        SetAction(parsedResult =>
        {
            string portName = parsedResult.GetRequiredValue(portNameOption);
            FileInfo? reportFile = parsedResult.GetValue(fileOption);
            bool verbose = parsedResult.GetValue(verboseOption);
            bool prerelease = parsedResult.GetValue(prereleaseOption);
            string? clockPort = parsedResult.GetValue(clockPortOption);
            ClockTestOptions? clockOptions = clockPort is null ? null : new ClockTestOptions(
                ClockPort: clockPort,
                PpsEvent: parsedResult.GetValue(ppsEventOption),
                ClockSamples: parsedResult.GetValue(clockSamplesOption));
            FileInfo? metadataPath = parsedResult.GetValue(metadataOption);
            return RunVerification(portName, reportFile, verbose, prerelease, clockOptions, metadataPath, CancellationToken.None);
        });
    }

    static async Task RunVerification(string portName, FileInfo? reportFile, bool verbose, bool prerelease, ClockTestOptions? clockOptions, FileInfo? metadataPath, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine($"Running tests on [bold]{portName}[/]...");
        if (clockOptions is not null)
            AnsiConsole.MarkupLine($"Clock reference device: [bold]{clockOptions.ClockPort}[/]");

        DeviceMetadata? deviceMetadata = null;
        string? deviceRawYaml = null;
        if (metadataPath is not null)
        {
            AnsiConsole.Markup($"Loading device metadata from [bold]{metadataPath.FullName}[/]...");
            deviceMetadata = GeneratorHelper.ReadDeviceMetadata(metadataPath.FullName);
            deviceRawYaml = await File.ReadAllTextAsync(metadataPath.FullName, cancellationToken);
            AnsiConsole.MarkupLine($" [green]Done![/] ({deviceMetadata.Registers.Count} registers)");
        }

        using var connection = await VerifyConnection.OpenAsync(portName, cancellationToken);
        var identity = await connection.ReadDeviceIdentityAsync(cancellationToken);
        var target = await ProtocolTarget.ResolveAsync(connection, prerelease, cancellationToken);
        var runner = new CoreRunner(target.IncludePrerelease, clockOptions, deviceMetadata, deviceRawYaml);
        var notice = GetProtocolNotice(target, runner.PrereleaseTestCount);

        AnsiConsole.MarkupLine(DescribeDeviceIdentity(identity, portName));
        AnsiConsole.MarkupLine(DescribeProtocolSelection(target));
        if (notice.Length > 0)
        {
            var style = target.IncludePrerelease ? "grey" : "yellow";
            AnsiConsole.MarkupLine($"[{style}]{Markup.Escape(notice)}[/]");
        }

        var report = new Report
        {
            DeviceName = identity.Name is { Length: > 0 } name ? name : "Harp Device",
            PortName = portName,
            WhoAmI = identity.WhoAmI.ToString(),
            HardwareVersion = identity.HardwareVersion?.ToString() ?? "not reported",
            FirmwareVersion = identity.FirmwareVersion?.ToString() ?? "not reported",
            RunDate = DateTime.Now,
            IncludePrerelease = target.IncludePrerelease,
            ProtocolNotice = notice,
            DeclaredProtocolVersion = GetDeclaredVersion(target),
            CheckedProtocolVersion = GetCheckedVersion(target),
            RegisterSetVersion = CoreSchema.Version
        };

        int currentTest = 0;
        await foreach (var (suite, result) in runner.RunAllAsync(connection, cancellationToken, (suite, testName, testDesc) =>
        {
            // Print "Running" status before test execution (without newline)
            currentTest++;
            if (!Console.IsOutputRedirected)
                Console.Write($"({currentTest}/{runner.TestCount}) {suite.GetType().Name}::{testName} .... Running...");
        }))
        {
            // Clear the line by moving cursor to start and overwriting with spaces, then print result
            if (!Console.IsOutputRedirected)
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
                        details = $"Mean: {bsr.Summary.Mean:F4}\nMedian: {bsr.Summary.Median:F4}\nStdDev: {bsr.Summary.StdDev:F4}\nMin: {bsr.Summary.Min:F4}\nMax: {bsr.Summary.Max:F4}\nPercentiles: 99th={bsr.Summary.Percentile99:F4}, 1st={bsr.Summary.Percentile01:F4}";
                    }
                    else if (test.Result is ErrorResult er)
                    {
                        details = $"{er.Exception.GetType().Name}";
                    }
                    else
                    {
                        var valProp = test.Result.GetType().GetProperty("Value");
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
            string fileName = reportFile.FullName;
            await File.WriteAllTextAsync(fileName, html, cancellationToken);
            AnsiConsole.MarkupLine($"[green]Done![/] Report generated: [link]{fileName}[/]");
        }
    }

    static string GetDeclaredVersion(ProtocolTarget target)
    {
        return target.DeclaredVersion.HasValue
            ? target.DeclaredVersion.GetValueOrDefault().ToString()
            : "not declared";
    }

    static string DescribeDeviceIdentity(DeviceIdentity identity, string portName)
    {
        var name = identity.Name is { Length: > 0 } deviceName ? deviceName : "unnamed device";
        return $"Device [bold]{Markup.Escape(name)}[/] on {portName}, WhoAmI [bold]{identity.WhoAmI}[/], " +
            $"hardware {identity.HardwareVersion?.ToString() ?? "not reported"}, " +
            $"firmware {identity.FirmwareVersion?.ToString() ?? "not reported"}.";
    }

    static string DescribeProtocolSelection(ProtocolTarget target)
    {
        if (target.IncludePrerelease)
        {
            return $"Checking against protocol version [bold]{GetDeclaredVersion(target)}[/], " +
                "which is not yet ratified.";
        }

        return $"Protocol version [bold]{GetDeclaredVersion(target)}[/], " +
            $"checking against [bold]{GetCheckedVersion(target)}[/].";
    }

    static string GetCheckedVersion(ProtocolTarget target)
    {
        if (target.IncludePrerelease)
            return $"{GetDeclaredVersion(target)}, which is not yet ratified";

        return target.Scope == ProtocolScope.V2
            ? "v1, since v2 is not yet ratified"
            : "v1";
    }

    static string GetProtocolNotice(ProtocolTarget target, int count)
    {
        if (target.Scope == ProtocolScope.Unsupported)
        {
            return $"This device declares protocol {GetDeclaredVersion(target)}, which this toolkit " +
                "does not cover. The results below are against the v1 baseline only.";
        }

        if (target.Scope == ProtocolScope.V1)
        {
            if (target.DeclaredVersion.HasValue)
                return $"This device declares protocol {GetDeclaredVersion(target)}, so only the v1 baseline applies.";

            var recommendation = "Updating to a firmware that implements R_VERSION would let it be " +
                "verified against the current protocol.";
            return target.PrereleaseRequested
                ? $"This device declares no protocol version, so --prerelease had no effect. {recommendation}"
                : $"This device declares no protocol version. {recommendation}";
        }

        if (count == 0)
            return string.Empty;

        return target.PrereleaseRequested
            ? $"Including {count} prerelease checks, which this device declares support for."
            : $"{count} prerelease checks were not run. Rerun with --prerelease to include them.";
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
            bool includePrerelease,
            ClockTestOptions? clockOptions = null,
            DeviceMetadata? deviceMetadata = null,
            string? deviceRawYaml = null) : base(includePrerelease)
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


