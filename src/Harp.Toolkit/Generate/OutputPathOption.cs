using System.CommandLine;

namespace Harp.Toolkit.Generate;

public class OutputPathOption : Option<DirectoryInfo>
{
    public OutputPathOption()
        : base("-o", "--output")
    {
        Description = "Location to place the generated output.";
        DefaultValueFactory = _ => new DirectoryInfo(".");
    }
}
