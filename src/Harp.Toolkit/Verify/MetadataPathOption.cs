using System.CommandLine;
using Harp.Generators;
using YamlDotNet.Core;

namespace Harp.Toolkit.Verify;

public class MetadataPathOption : Option<FileInfo>
{
    public MetadataPathOption()
        : base("--metadata")
    {
        OptionValidation.AcceptExistingOnly(this);
        Description = "The path to the file that describes the device registers. Enables device interface tests.";
    }

    public static DeviceMetadata Parse(FileInfo metadataPath, string text)
    {
        try
        {
            return DeviceMetadata.Parse(text);
        }
        catch (YamlException ex)
        {
            ex.SetDescription(MetadataErrors.Describe(metadataPath, ex));
            throw;
        }
    }
}
