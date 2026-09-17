using System.CommandLine;
using Spectre.Console;
using Spectre.Console.Rendering;
using Harp.Toolkit.Firmware;
using Harp.Toolkit.Firmware.ATxmega;

namespace Harp.Toolkit;

public class UpdateFirmwareCommand : Command
{
    const string FirmwareNameHint =
        "The name carries the device and version numbers, as in " +
        "<device>-fw<firmware>-harp<core>-hw<hardware>-ass<assembly>.hex.";

    const string SupportedFirmwareHint =
        "The update writes Intel HEX images to devices built on the ATxmega core. Devices built " +
        "on other cores update through a different process.";

    const string InterruptedUpdateHint =
        "The update may have left the device in bootloader mode. Run the update again, and if the " +
        "device does not respond, power cycle it and re-run with --force, since a device in " +
        "bootloader mode cannot report its identity for the compatibility check.";

    const string NoResponseHint =
        "No device answered the bootloader protocol. The device can still be restarting from a " +
        "previous operation, or it can be on a different port.";

    const string BootloaderModeHint =
        "The device is in bootloader mode, left there by an interrupted update. Re-run with " +
        "--force, which skips the compatibility check when the device cannot answer.";

    const string NotReadyHint =
        "The device may still be restarting. Run 'harp.toolkit --port <port>' to check it before " +
        "updating again.";

    const int ReadyTimeoutMilliseconds = 20000;

    static readonly int StageWidth = Enum.GetValues<UpdateStage>().Max(stage => DescribeStage(stage).Length);

    static string DescribeStage(UpdateStage stage)
    {
        return stage switch
        {
            UpdateStage.Connect => "Connecting to the device",
            UpdateStage.Check => "Checking compatibility",
            UpdateStage.Reset => "Resetting the device",
            UpdateStage.Bootloader => "Waiting for bootloader",
            UpdateStage.Write => "Writing the image",
            UpdateStage.Restart => "Restarting the device",
            _ => "Updating firmware"
        };
    }

    sealed class StageColumn : ProgressColumn
    {
        public override int? GetColumnWidth(RenderOptions options) => StageWidth;

        public override IRenderable Render(RenderOptions options, ProgressTask task, TimeSpan deltaTime)
        {
            return new Markup(Markup.Escape(task.Description)).Overflow(Overflow.Ellipsis).LeftJustified();
        }
    }

    static bool TryDescribeInterruption(Exception exception, int percent, out string message)
    {
        switch (exception)
        {
            case FileNotFoundException:
            case UnauthorizedAccessException:
            case InvalidOperationException:
            case OperationCanceledException:
                message = $"The connection to the device was lost while updating at {percent}%.";
                return true;
            case TimeoutException:
                message = $"The device stopped responding while updating at {percent}%.";
                return true;
            case Bonsai.Harp.HarpException:
                message = $"{exception.Message} The update stopped at {percent}%.";
                return true;
            default:
                message = $"The update failed at {percent}%. {exception.GetType().Name}: {exception.Message}";
                return true;
        }
    }

    public UpdateFirmwareCommand()
        : base("update", "Update the device firmware from a local HEX file.")
    {
        DevicePortNameOption portNameOption = new();
        PortTimeoutOption portTimeoutOption = new();
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
            Description = "Force a firmware update, skipping the compatibility check when the device cannot answer."
        };

        Arguments.Add(firmwareArgument);
        Options.Add(portNameOption);
        Options.Add(portTimeoutOption);
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

        SetAction((parseResult, cancellationToken) =>
        {
            var firmwarePath = parseResult.GetValue(firmwareArgument) ?? parseResult.GetValue(firmwarePathOption)!;
            var portName = parseResult.GetRequiredValue(portNameOption);
            var portTimeout = parseResult.GetRequiredValue(portTimeoutOption);
            var forceUpdate = parseResult.GetValue(forceUpdateOption);

            return portNameOption.ReportErrorsAsync(portName, async () =>
            {
                if (!string.Equals(firmwarePath.Extension, ".hex", StringComparison.OrdinalIgnoreCase))
                {
                    Console.Error.WriteLine(
                        $"The firmware file {firmwarePath.Name} is not an Intel HEX image. {SupportedFirmwareHint}");
                    return 1;
                }

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
                var enteredBootloader = false;
                var lastPercent = 0;
                try
                {
                    await AnsiConsole.Progress().AutoClear(true).Columns(
                        new StageColumn(),
                        new ProgressBarColumn(),
                        new PercentageColumn()).StartAsync(async context =>
                    {
                        var task = context.AddTask("Updating firmware");
                        var progress = new ImmediateProgress<UpdateProgress>(update =>
                        {
                            enteredBootloader |= update.Stage == UpdateStage.Bootloader;
                            lastPercent = update.Percent;
                            task.Description = DescribeStage(update.Stage);
                            task.Value = update.Percent;
                        });
                        await Bootloader.UpdateFirmwareAsync(
                            portName, firmware, forceUpdate, portTimeout, progress, cancellationToken);
                    });
                }
                catch (OperationCanceledException) when (!enteredBootloader)
                {
                    Console.Error.WriteLine("The update was canceled before the device was reset.");
                    return 1;
                }
                catch (Exception ex) when (enteredBootloader &&
                                           TryDescribeInterruption(ex, lastPercent, out var interruption))
                {
                    Console.Error.WriteLine($"{interruption} {InterruptedUpdateHint}");
                    return 1;
                }
                catch (TimeoutException ex) when (!forceUpdate &&
                                                 portNameOption.TryDescribe(ex, portName, out var cause))
                {
                    var hint = await Bootloader.IsBootloaderAsync(portName) ? BootloaderModeHint : NoResponseHint;
                    Console.Error.WriteLine($"{cause} {hint}");
                    return 1;
                }

                bool ready;
                try
                {
                    ready = await AnsiConsole.Status().StartAsync(
                        "Waiting for the device to restart",
                        _ => FirmwareUpdate.WaitUntilReadyAsync(portName, ReadyTimeoutMilliseconds, cancellationToken));
                }
                catch (OperationCanceledException)
                {
                    Console.Error.WriteLine(
                        $"The firmware was written, and the wait for the device on {portName} was " +
                        "canceled. The device may still be restarting.");
                    return 1;
                }

                if (!ready)
                {
                    Console.Error.WriteLine(
                        $"The firmware was written, but the device on {portName} did not answer " +
                        $"in time. {NotReadyHint}");
                    return 1;
                }

                Console.WriteLine("Firmware updated.");
                return 0;
            });
        });
    }
}
