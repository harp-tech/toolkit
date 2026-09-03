using System.CommandLine;
using Bonsai.Harp;
using Harp.Toolkit.Generate;

namespace Harp.Toolkit;

internal class Program
{
    static async Task<int> Main(string[] args)
    {
        RootCommand rootCommand = new("Tool for inspecting, updating and interfacing with Harp devices.");
        PortNameOption portNameOption = new();
        PortTimeoutOption portTimeoutOption = new();
        rootCommand.Options.Add(portNameOption);
        rootCommand.Options.Add(portTimeoutOption);
        rootCommand.Subcommands.Add(new ListCommand());
        rootCommand.Subcommands.Add(new UpdateFirmwareCommand());
        rootCommand.Subcommands.Add(new GenerateCommand());
        rootCommand.Subcommands.Add(new BenchmarkCommand());
        rootCommand.SetAction(async parseResult =>
        {
            var portName = parseResult.GetRequiredValue(portNameOption);
            var portTimeout = parseResult.GetRequiredValue(portTimeoutOption);

            using var device = new AsyncDevice(portName);
            var whoAmI = await device.ReadWhoAmIAsync().WithTimeout(portTimeout);
            var hardwareVersion = await device.ReadHardwareVersionAsync().WithTimeout(portTimeout);
            var firmwareVersion = await device.ReadFirmwareVersionAsync().WithTimeout(portTimeout);
            var timestamp = await device.ReadTimestampSecondsAsync().WithTimeout(portTimeout);
            var deviceName = await device.ReadDeviceNameAsync().WithTimeout(portTimeout);
            Console.WriteLine($"Harp device found in {portName}");
            Console.WriteLine($"DeviceName: {deviceName}");
            Console.WriteLine($"WhoAmI: {whoAmI}");
            Console.WriteLine($"Hw: {hardwareVersion.Major}.{hardwareVersion.Minor}");
            Console.WriteLine($"Fw: {firmwareVersion.Major}.{firmwareVersion.Minor}");
            Console.WriteLine($"Timestamp (s): {timestamp}");
            Console.WriteLine();
        });

        var parseResult = rootCommand.Parse(args);
        return await parseResult.InvokeAsync();
    }
}
