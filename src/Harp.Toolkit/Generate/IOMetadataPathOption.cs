using System.CommandLine;
using Harp.Generators;
using YamlDotNet.Core;

namespace Harp.Toolkit.Generate;

public class IOMetadataPathOption : Option<FileInfo>
{
    public IOMetadataPathOption()
        : base("--ios")
    {
        OptionValidation.AcceptExistingOnly(this);
        Description = "The path to the file that describes the device IO pins.";
        DefaultValueFactory = result => result.AcceptExistingOnly(new FileInfo("ios.yml"));
    }

    public PortPinMetadata Load(ParseResult parseResult)
    {
        var iosMetadataPath = parseResult.GetRequiredValue(this);
        try
        {
            return PortPinMetadata.Load(iosMetadataPath.FullName);
        }
        catch (YamlException ex)
        {
            ex.SetDescription(MetadataErrors.Describe(iosMetadataPath, ex));
            throw;
        }
    }
}
