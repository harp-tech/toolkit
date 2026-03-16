using System.CommandLine;

namespace Harp.Toolkit.Generate;

public class GenerateCommand : Command
{
    public GenerateCommand()
        : base("generate", "Generate firmware or interface code for Harp devices.")
    {
        Subcommands.Add(new GenerateInterfaceCommand());
        Subcommands.Add(new GenerateFirmwareCommand());
    }

    internal static void WriteFileContents(string path, IEnumerable<KeyValuePair<string, string>> generatedFileContents)
    {
        foreach ((var fileName, var fileContents) in generatedFileContents)
        {
            Console.WriteLine($"Generating {fileName}...");
            File.WriteAllText(Path.Combine(path, fileName), fileContents);
        }
    }
}
