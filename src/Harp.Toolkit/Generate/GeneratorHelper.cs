using System.CodeDom.Compiler;
using System.Text;
using Harp.Generators;
using YamlDotNet.Core;

namespace Harp.Toolkit.Generate;

public static class GeneratorHelper
{
    public static DeviceInfo ReadDeviceMetadata(string path)
    {
        using var reader = new StreamReader(path);
        var parser = new MergingParser(new Parser(reader));
        return MetadataDeserializer.Instance.Deserialize<DeviceInfo>(parser);
    }

    public static Dictionary<string, PortPinInfo> ReadPortPinMetadata(string path)
    {
        using var reader = new StreamReader(path);
        return MetadataDeserializer.Instance.Deserialize<Dictionary<string, PortPinInfo>>(reader);
    }

    public static IEnumerable<KeyValuePair<string, T>> GetPortPinsOfType<T>(IDictionary<string, PortPinInfo> portPins) where T : PortPinInfo
    {
        return from item in portPins
               where item.Value is T
               select new KeyValuePair<string, T>(item.Key, (T)item.Value);
    }

    public static bool AssertNoGeneratorErrors(CompilerErrorCollection errors)
    {
        if (errors.Count > 0)
        {
            var errorLog = new StringBuilder();
            errorLog.AppendLine("Code generation has completed with errors:");
            foreach (CompilerError error in errors)
            {
                var warningString = error.IsWarning ? "warning" : "error";
                errorLog.AppendLine($"{error.FileName}: {warningString}: {error.ErrorText}");
            }
            Console.Error.WriteLine(errorLog.ToString());
            return !errors.HasErrors;
        }
        
        return true;
    }
}
