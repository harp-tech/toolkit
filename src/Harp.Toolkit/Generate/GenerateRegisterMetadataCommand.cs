using System.CommandLine;
using System.Data;
using Bonsai.Harp;
using ExcelDataReader;
using Harp.Generators;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Harp.Toolkit.Generate;

class GenerateRegisterMetadataCommand : Command
{
    public GenerateRegisterMetadataCommand()
        : base("metadata", "Generate device register metadata file from legacy XLS files.")
    {
        OutputPathOption outputPathOption = new();
        Argument<FileInfo> registerWorksheetPathArgument = ArgumentValidation.AcceptExistingOnly(
            new Argument<FileInfo>("registers.xls")
        {
            Description = "The path to the file describing the device registers.",
            Arity = ArgumentArity.ExactlyOne
        });

        Arguments.Add(registerWorksheetPathArgument);
        Options.Add(outputPathOption);

        SetAction(parseResult =>
        {
            var outputPath = parseResult.GetRequiredValue(outputPathOption);
            var registerWorksheetPath = parseResult.GetRequiredValue(registerWorksheetPathArgument);
            var deviceInfo = new DeviceInfo();
            var iosMetadata = new Dictionary<string, PortPinInfo>();

            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            using var sourceStream = File.Open(registerWorksheetPath.FullName, FileMode.Open, FileAccess.Read);
            using var reader = ExcelReaderFactory.CreateReader(sourceStream);
            var result = reader.AsDataSet();
            foreach (DataTable table in result.Tables)
            {
                ParseMetadataTable(table, deviceInfo);
                ParseRegisterTable(table, deviceInfo);
                ParseIosTable(table, iosMetadata);
            }

            var registerMetadataPath = Path.Combine(outputPath.FullName, "device.yml");
            var iosMetadataPath = Path.Combine(outputPath.FullName, "ios.yml");
            File.WriteAllText(registerMetadataPath, MetadataSerializer.Instance.Serialize(deviceInfo));
            File.WriteAllText(iosMetadataPath, MetadataSerializer.Instance.Serialize(iosMetadata));
        });
    }

    static void ParseMetadataTable(DataTable table, DeviceInfo deviceInfo)
    {
        if (table.TableName != "meta data")
            return;

        deviceInfo.WhoAmI = Convert.ToUInt16(table.Rows[0][1]);
        var fwMajor = Convert.ToUInt16(table.Rows[1][1]);
        var fwMinor = Convert.ToUInt16(table.Rows[2][1]);
        deviceInfo.FirmwareVersion = new HarpVersion(fwMajor, fwMinor);
        var hwMajor = Convert.ToUInt16(table.Rows[3][1]);
        var hwMinor = Convert.ToUInt16(table.Rows[4][1]);
        deviceInfo.HardwareTargets = new HarpVersion(hwMajor, hwMinor);
        deviceInfo.Device = (string)table.Rows[6][1];
    }

    static void ParseRegisterTable(DataTable table, DeviceInfo deviceInfo)
    {
        if (table.TableName != "registers")
            return;

        var registerOffset = 32;
        for (int i = 1; i < table.Rows.Count; i++)
        {
            var row = table.Rows[i];
            var registerName = row[5] as string;
            if (string.IsNullOrEmpty(registerName))
                continue;

            registerName = FirmwareNamingConvention.Instance.Reverse(registerName);
            var registerInfo = new RegisterInfo
            {
                Address = registerOffset++,
                Type = (PayloadType)Enum.Parse(typeof(PayloadType), ((string)row[2]).Replace("I", "S"), ignoreCase: true),
                Description = row[6] as string ?? string.Empty
            };
            deviceInfo.Registers[registerName] = registerInfo;
        }
    }

    static InputPinMode ParseInputPinMode(string value)
    {
        return value switch
        {
            "up" => InputPinMode.PullUp,
            "down" => InputPinMode.PullDown,
            "tristate" => InputPinMode.TriState,
            "busholder" => InputPinMode.BusHolder,
            _ => throw new ArgumentException("Invalid input pin mode.", nameof(value))
        };
    }

    static OutputPinMode ParseOutputPinMode(string value)
    {
        return value switch
        {
            "digital" => OutputPinMode.Digital,
            "wire_or" => OutputPinMode.WiredOr,
            "wire_and" => OutputPinMode.WiredAnd,
            "wired_or_pull" => OutputPinMode.WiredOrPull,
            "wired_and_pull" => OutputPinMode.WiredAndPull,
            _ => throw new ArgumentException("Invalid output pin mode.", nameof(value))
        };
    }

    static InterruptPriority ParseInterruptPriority(string value)
    {
        return value switch
        {
            "off" => InterruptPriority.Off,
            "low" => InterruptPriority.Low,
            "med" => InterruptPriority.Medium,
            "high" => InterruptPriority.High,
            _ => throw new ArgumentException("Invalid interrupt priority.", nameof(value))
        };
    }

    static TriggerMode ParseTriggerMode(string value)
    {
        return value switch
        {
            "rising_edge" => TriggerMode.Rising,
            "falling_edge" => TriggerMode.Falling,
            "both_edges" => TriggerMode.Toggle,
            "low_level" => TriggerMode.Low,
            "no_interrupt" => TriggerMode.None,
            _ => throw new ArgumentException("Invalid trigger mode.", nameof(value))
        };
    }

    static void ParseIosTable(DataTable table, Dictionary<string, PortPinInfo> iosMetadata)
    {
        if (table.TableName != "ios")
            return;

        for (int i = 1; i < table.Rows.Count; i++)
        {
            var row = table.Rows[i];
            var pinName = row[0] as string;
            if (string.IsNullOrEmpty(pinName))
                continue;

            var direction = (PinDirection)Enum.Parse(typeof(PinDirection), (string)row[3], ignoreCase: true);
            PortPinInfo pinInfo = direction switch
            {
                PinDirection.Input => new InputPinInfo
                {
                    TriggerMode = ParseTriggerMode((string)row[8]),
                    InterruptPriority = ParseInterruptPriority((string)row[9]),
                    InterruptNumber = row[10] is not DBNull ? Convert.ToInt32(row[10]) : null,
                    PinMode = ParseInputPinMode((string)row[7])
                },
                _ => new OutputPinInfo
                {
                    AllowRead = (string)row[4] == "yes",
                    PinMode = ParseOutputPinMode((string)row[5]),
                    InitialState = Convert.ToInt32(row[6]) == 0 ? LogicState.Low : LogicState.High,
                    Invert = (string)row[12] == "yes"
                }
            };

            pinInfo.Direction = direction;
            pinInfo.Port = (string)row[1];
            pinInfo.PinNumber = Convert.ToInt32(row[2]);
            pinInfo.Description = row[11] as string ?? string.Empty;
            iosMetadata[pinName] = pinInfo;
        }
    }
}
