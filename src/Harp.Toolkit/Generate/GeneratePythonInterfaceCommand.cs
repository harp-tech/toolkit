using System.CommandLine;
using Harp.Generators;

namespace Harp.Toolkit.Generate;

class GeneratePythonInterfaceCommand : Command
{
    public GeneratePythonInterfaceCommand()
        : base("python", "Generate the Harp Python device interface.")
    {
        MetadataPathArgument metadataPathArgument = new();
        OutputPathOption outputPathOption = new();

        Option<bool> packageOption = new("--package")
        {
            Description = "Generate the interface as a package."
        };

        Arguments.Add(metadataPathArgument);
        Options.Add(packageOption);
        Options.Add(outputPathOption);

        SetAction(parseResult =>
        {
            var outputPath = parseResult.GetRequiredValue(outputPathOption);
            var metadataPath = parseResult.GetRequiredValue(metadataPathArgument);
            var package = parseResult.GetValue(packageOption);

            var deviceMetadata = DeviceMetadata.Load(metadataPath.FullName);
            var generator = new PythonGenerator(deviceMetadata, package);
            var implementation = generator.GenerateImplementation();
            if (GeneratorHelper.AssertNoGeneratorErrors(generator.Errors))
                GenerateCommand.WriteFileContents(outputPath.FullName, implementation);
        });
    }
}
