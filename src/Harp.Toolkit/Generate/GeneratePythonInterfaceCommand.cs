using System.CommandLine;
using Harp.Generators;

namespace Harp.Toolkit.Generate;

class GeneratePythonInterfaceCommand : Command
{
    public GeneratePythonInterfaceCommand(
        Argument<FileInfo> metadataPathArgument,
        Option<string> namespaceOption,
        Option<DirectoryInfo> outputPathOption)
        : base("python", "Generate the Harp Python device interface.")
    {
        Validators.Add(commandResult =>
        {
            var namespaceResult = commandResult.GetResult(namespaceOption);
            if (namespaceResult is not null && !namespaceResult.Implicit)
                commandResult.AddError("The --namespace option does not apply to the Python interface.");
        });

        SetAction(parseResult =>
        {
            var outputPath = parseResult.GetRequiredValue(outputPathOption);
            var metadataPath = parseResult.GetRequiredValue(metadataPathArgument);

            var deviceMetadata = GeneratorHelper.ReadDeviceMetadata(metadataPath.FullName);
            var generator = new PythonGenerator(deviceMetadata);
            var implementation = generator.GenerateImplementation();
            if (GeneratorHelper.AssertNoGeneratorErrors(generator.Errors))
                GenerateCommand.WriteFileContents(outputPath.FullName, implementation);
        });
    }
}
