using System.CommandLine;
using Bonsai.Harp;

namespace Harp.Toolkit;

public class UpdateFirmwareCommand : Command
{
    public UpdateFirmwareCommand()
        : base("update", "Update the device firmware from a local HEX file.")
    {
        DevicePortNameOption portNameOption = new();
        Option<FileInfo> firmwarePathOption = new("--path")
        {
            Description = "Specifies the path of the firmware file to write to the device.",
            Required = true
        };

        Option<bool> forceUpdateOption = new("--force")
        {
            Description = "Indicates whether to force a firmware update on the device regardless of compatibility."
        };

        Options.Add(portNameOption);
        Options.Add(firmwarePathOption);
        Options.Add(forceUpdateOption);
        SetAction(parseResult =>
        {
            var firmwarePath = parseResult.GetRequiredValue(firmwarePathOption);
            var portName = parseResult.GetRequiredValue(portNameOption);
            var forceUpdate = parseResult.GetValue(forceUpdateOption);

            var firmware = DeviceFirmware.FromFile(firmwarePath.FullName);
            Console.WriteLine($"{firmware.Metadata}");
            return portNameOption.ReportErrorsAsync(portName, async () =>
            {
                ProgressBar.Write(0);
                try
                {
                    var progress = new Progress<int>(ProgressBar.Update);
                    await Bootloader.UpdateFirmwareAsync(portName, firmware, forceUpdate, progress);
                }
                finally { Console.WriteLine(); }
            });
        });
    }
}
