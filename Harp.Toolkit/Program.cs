using System.CommandLine;
using System.IO.Ports;
using Bonsai.Harp;

namespace Harp.Toolkit;

internal class Program
{
    static async Task Main(string[] args)
    {
        RootCommand rootCommand = new("Tool for inspecting, updating and interfacing with Harp devices.");
        PortNameOption portNameOption = new();
        PortTimeoutOption portTimeoutOption = new();
        rootCommand.Options.Add(portNameOption);
        rootCommand.Options.Add(portTimeoutOption);
        rootCommand.Subcommands.Add(new ListCommand());
        rootCommand.Subcommands.Add(new UpdateFirmwareCommand());
        rootCommand.SetAction(async parseResult =>
        {
            var portName = parseResult.GetRequiredValue(portNameOption);
            var portTimeout = parseResult.GetValue(portTimeoutOption);

            using var device = new AsyncDevice(portName);
            var whoAmI = await device.ReadWhoAmIAsync().WithTimeout(portTimeout);
            var hardwareVersion = await device.ReadHardwareVersionAsync();
            var firmwareVersion = await device.ReadFirmwareVersionAsync();
            var timestamp = await device.ReadTimestampSecondsAsync();
            var deviceName = await device.ReadDeviceNameAsync();
            Console.WriteLine($"Harp device found in {portName}");
            Console.WriteLine($"DeviceName: {deviceName}");
            Console.WriteLine($"WhoAmI: {whoAmI}");
            Console.WriteLine($"Hw: {hardwareVersion.Major}.{hardwareVersion.Minor}");
            Console.WriteLine($"Fw: {firmwareVersion.Major}.{firmwareVersion.Minor}");
            Console.WriteLine($"Timestamp (s): {timestamp}");
            Console.WriteLine();
        });

        var parseResult = rootCommand.Parse(args);
        await parseResult.InvokeAsync();
    }
}
