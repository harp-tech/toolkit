using System.CommandLine;

namespace Harp.Toolkit.Generate;

public class MetadataPathArgument : Argument<FileInfo>
{
    public MetadataPathArgument()
        : base("metadataPath")
    {
        ArgumentValidation.AcceptExistingOnly(this);
        Description = "The path to the file describing the device registers.";
        DefaultValueFactory = result => result.AcceptExistingOnly(new FileInfo("device.yml"));
        Arity = ArgumentArity.ZeroOrOne;
    }
}
