using System.CommandLine;

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
}
