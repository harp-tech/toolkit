using System.CommandLine;
using Harp.Generators;
using YamlDotNet.Core;

namespace Harp.Toolkit.Generate;

public class MetadataPathArgument : Argument<FileInfo>
{
    public MetadataPathArgument()
        : base("metadata")
    {
        ArgumentValidation.AcceptExistingOnly(this);
        Description = "The path to the file that describes the device registers.";
        DefaultValueFactory = result => result.AcceptExistingOnly(new FileInfo("device.yml"));
        Arity = ArgumentArity.ZeroOrOne;
    }

    public DeviceMetadata Load(ParseResult parseResult)
    {
        var metadataPath = parseResult.GetRequiredValue(this);
        try
        {
            return DeviceMetadata.Load(metadataPath.FullName);
        }
        catch (YamlException ex)
        {
            ex.SetDescription(MetadataErrors.Describe(metadataPath, ex));
            throw;
        }
    }
}
