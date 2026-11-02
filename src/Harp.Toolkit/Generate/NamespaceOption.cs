using System.CommandLine;
using Harp.Generators;

namespace Harp.Toolkit.Generate;

public class NamespaceOption : Option<string>
{
    public NamespaceOption()
        : base("-ns", "--namespace")
    {
        Description = "The namespace for the generated code. The default is `Harp.DeviceName`.";
    }

    public string GetValue(ParseResult parseResult, DeviceMetadata deviceMetadata)
    {
        return parseResult.GetValue(this) ?? $"Harp.{deviceMetadata.Device}";
    }
}
