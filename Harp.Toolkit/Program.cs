using System.CommandLine;
using System.IO.Ports;
using Bonsai.Harp;

namespace Harp.Toolkit;

internal class Program
{
    static async Task Main(string[] args)
    {
        Option<string> portNameOption = new("--port")
        {
            Description = "Specifies the name of the serial port used to communicate with the device.",
            Required = true
        };

        Option<int?> portTimeoutOption = new("--timeout")
        {
            Description = "Specifies an optional timeout, in milliseconds, to receive a response from the device."
        };

        Option<FileInfo> firmwarePathOption = new("--path")
        {
            Description = "Specifies the path of the firmware file to write to the device.",
            Required = true
        };

        Option<bool> forceUpdateOption = new("--force")
        {
            Description = "Indicates whether to force a firmware update on the device regardless of compatibility."
        };

        var listCommand = new Command("list", description: "Lists all available system serial ports.");
        listCommand.SetAction(parseResult =>
        {
            var portNames = SerialPort.GetPortNames();
            Console.WriteLine($"PortNames: [{string.Join(", ", portNames)}]");
        });

        var updateCommand = new Command("update", description: "Update the device firmware from a local HEX file.");
        updateCommand.Options.Add(portNameOption);
        updateCommand.Options.Add(firmwarePathOption);
        updateCommand.Options.Add(forceUpdateOption);
        updateCommand.SetAction(async parseResult =>
        {
            var firmwarePath = parseResult.GetRequiredValue(firmwarePathOption);
            var portName = parseResult.GetRequiredValue(portNameOption);
            var forceUpdate = parseResult.GetValue(forceUpdateOption);

            var firmware = DeviceFirmware.FromFile(firmwarePath.FullName);
            Console.WriteLine($"{firmware.Metadata}");
            ProgressBar.Write(0);
            try
            {
                var progress = new Progress<int>(ProgressBar.Update);
                await Bootloader.UpdateFirmwareAsync(portName, firmware, forceUpdate, progress);
            }
            finally { Console.WriteLine(); }
        });

        var rootCommand = new RootCommand("Tool for inspecting, updating and interfacing with Harp devices.");
        rootCommand.Options.Add(portNameOption);
        rootCommand.Options.Add(portTimeoutOption);
        rootCommand.Subcommands.Add(listCommand);
        rootCommand.Subcommands.Add(updateCommand);
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
