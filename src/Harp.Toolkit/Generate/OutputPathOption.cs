using System.CommandLine;

namespace Harp.Toolkit.Generate;

public class OutputPathOption : Option<DirectoryInfo>
{
    public OutputPathOption()
        : base("-o", "--output")
    {
        Description = "Location to place the generated output. The default is the current directory.";
        DefaultValueFactory = _ => new DirectoryInfo(Environment.CurrentDirectory);
    }
}
