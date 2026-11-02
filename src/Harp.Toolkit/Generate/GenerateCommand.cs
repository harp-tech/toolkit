using System.CommandLine;

namespace Harp.Toolkit.Generate;

public class GenerateCommand : Command
{
    public GenerateCommand()
        : base("generate", "Generate firmware code, interface code or documentation for Harp devices.")
    {
        Subcommands.Add(new GenerateInterfaceCommand());
        Subcommands.Add(new GenerateFirmwareCommand());
        Subcommands.Add(new GenerateDocumentationCommand());
        Subcommands.Add(new GenerateRegisterMetadataCommand());
    }

    internal static void WriteFileContents(string path, IEnumerable<KeyValuePair<string, string>> generatedFileContents)
    {
        Directory.CreateDirectory(path);
        foreach ((var fileName, var fileContents) in generatedFileContents)
        {
            Console.WriteLine($"Generating {fileName}...");
            File.WriteAllText(Path.Combine(path, fileName), fileContents);
        }
    }
}
