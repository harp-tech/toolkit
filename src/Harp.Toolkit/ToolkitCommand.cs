using System.CommandLine;
using Bonsai.Harp;
using Harp.Toolkit.Generate;
using Harp.Toolkit.Verify;

namespace Harp.Toolkit;

class ToolkitCommand : RootCommand
{
    public ToolkitCommand()
        : base("Tool for inspecting, updating and interfacing with Harp devices.")
    {
        DevicePortNameOption portNameOption = new();
        PortTimeoutOption portTimeoutOption = new();
        Options.Add(portNameOption);
        Options.Add(portTimeoutOption);
        Subcommands.Add(new ListCommand());
        Subcommands.Add(new UpdateFirmwareCommand());
        Subcommands.Add(new GenerateCommand());
        Subcommands.Add(new VerifyCommand());
        SetAction(parseResult =>
        {
            var portName = parseResult.GetRequiredValue(portNameOption);
            var portTimeout = parseResult.GetRequiredValue(portTimeoutOption);
            return portNameOption.ReportErrorsAsync(portName, async () =>
            {
                using var device = new AsyncDevice(portName);
                var identity = await DeviceIdentity.ReadAsync(device, portTimeout);
                var timestamp = await device.ReadTimestampSecondsAsync().WithTimeout(portTimeout);
                Console.WriteLine($"Harp device found in {portName}");
                Console.WriteLine($"DeviceName: {identity.DeviceName}");
                Console.WriteLine($"WhoAmI: {identity.WhoAmI}");
                Console.WriteLine($"Hw: {identity.HardwareVersion.Major}.{identity.HardwareVersion.Minor}");
                Console.WriteLine($"Fw: {identity.FirmwareVersion.Major}.{identity.FirmwareVersion.Minor}");
                Console.WriteLine($"Timestamp (s): {timestamp}");
                Console.WriteLine();
            });
        });
    }
}
