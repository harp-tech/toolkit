using System.CommandLine;
using Spectre.Console;
using Harp.Toolkit.Firmware.ATxmega;

namespace Harp.Toolkit;

public class UpdateFirmwareCommand : Command
{
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

            var firmware = DeviceFirmware.FromFile(firmwarePath.FullName);
            Console.WriteLine($"{firmware.Metadata}");
            return portNameOption.ReportErrorsAsync(portName, async () =>
            {
                await AnsiConsole.Progress().StartAsync(async context =>
                {
                    var task = context.AddTask("Updating firmware");
                    var progress = new ImmediateProgress<int>(percent => task.Value = percent);
                    await Bootloader.UpdateFirmwareAsync(portName, firmware, forceUpdate, progress);
                });
                Console.WriteLine("Firmware updated.");
            });
        });
    }
}
