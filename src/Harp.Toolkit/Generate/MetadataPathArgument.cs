using System.CommandLine;

namespace Harp.Toolkit.Generate;

public class MetadataPathArgument : Argument<FileInfo>
{
    public MetadataPathArgument()
        : base("device.yml")
    {
        ArgumentValidation.AcceptExistingOnly(this);
        Description = "The path to the file describing the device registers.";
        Arity = ArgumentArity.ExactlyOne;
    }
}
