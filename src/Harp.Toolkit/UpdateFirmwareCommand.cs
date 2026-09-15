using System.CommandLine;
using Spectre.Console;
using Harp.Toolkit.Firmware.ATxmega;

namespace Harp.Toolkit;

public class UpdateFirmwareCommand : Command
{
    const string FirmwareNameHint =
        "The name carries the device and version numbers, as in " +
        "<device>-fw<firmware>-harp<core>-hw<hardware>-ass<assembly>.hex.";

    public UpdateFirmwareCommand()
        : base("update", "Update the device firmware from a local HEX file.")
    {
        DevicePortNameOption portNameOption = new();
        Argument<FileInfo> firmwareArgument = ArgumentValidation.AcceptExistingOnly(
            new Argument<FileInfo>("firmware")
            {
                Description = "Path to the firmware file.",
                Arity = ArgumentArity.ZeroOrOne
            });

        Option<FileInfo> firmwarePathOption = OptionValidation.AcceptExistingOnly(
            new Option<FileInfo>("--path")
            {
                Description = "Path to the firmware file.",
                Hidden = true
            });

        Option<bool> forceUpdateOption = new("--force")
        {
            Description = "Force a firmware update regardless of compatibility."
        };

        Arguments.Add(firmwareArgument);
        Options.Add(portNameOption);
        Options.Add(firmwarePathOption);
        Options.Add(forceUpdateOption);
        Validators.Add(result =>
        {
            var hasArgument = result.GetResult(firmwareArgument) is not null;
            var hasOption = result.GetResult(firmwarePathOption) is not null;
            if (!hasArgument && !hasOption)
                result.AddError("Required argument missing for command: 'update'.");
            else if (hasArgument && hasOption)
                result.AddError("The firmware file must be given either as an argument or with --path, not both.");
        });

        SetAction(parseResult =>
        {
            var firmwarePath = parseResult.GetValue(firmwareArgument) ?? parseResult.GetValue(firmwarePathOption)!;
            var portName = parseResult.GetRequiredValue(portNameOption);
            var forceUpdate = parseResult.GetValue(forceUpdateOption);

            return portNameOption.ReportErrorsAsync(portName, async () =>
            {
                if (!FirmwareMetadata.TryParse(Path.GetFileNameWithoutExtension(firmwarePath.Name), out var metadata))
                {
                    Console.Error.WriteLine(
                        $"The name of the firmware file {firmwarePath.Name} does not follow the Harp convention. " +
                        $"{FirmwareNameHint}");
                    return 1;
                }

                DeviceFirmware firmware;
                try
                {
                    firmware = DeviceFirmware.FromFile(metadata, firmwarePath.FullName);
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
                {
                    Console.Error.WriteLine(
                        $"The firmware file {firmwarePath.Name} is not a valid Intel HEX image. {ex.Message}");
                    return 1;
                }

                Console.WriteLine($"{firmware.Metadata}");
                await AnsiConsole.Progress().StartAsync(async context =>
                {
                    var task = context.AddTask("Updating firmware");
                    var progress = new ImmediateProgress<int>(percent => task.Value = percent);
                    await Bootloader.UpdateFirmwareAsync(portName, firmware, forceUpdate, progress);
                });
                Console.WriteLine("Firmware updated.");
                return 0;
            });
        });
    }
}
