using System.CommandLine;

namespace Harp.Toolkit.Generate;

public class IOMetadataPathOption : Option<FileInfo>
{
    public IOMetadataPathOption()
        : base("--ios")
    {
        OptionValidation.AcceptExistingOnly(this);
        Description = "The path to the file describing the device IO pins.";
        DefaultValueFactory = _ => new FileInfo("ios.yml");
    }
}
