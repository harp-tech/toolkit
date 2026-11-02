using System.CommandLine;
using Harp.Generators;

namespace Harp.Toolkit.Generate;

class GenerateCSharpInterfaceCommand : Command
{
    public GenerateCSharpInterfaceCommand()
        : base("csharp", "Generate reactive programming API and async API.")
    {
        MetadataPathArgument metadataPathArgument = new();
        NamespaceOption namespaceOption = new();
        OutputPathOption outputPathOption = new();

        Arguments.Add(metadataPathArgument);
        Options.Add(namespaceOption);
        Options.Add(outputPathOption);

        SetAction(parseResult =>
        {
            var outputPath = parseResult.GetRequiredValue(outputPathOption);

            var deviceMetadata = metadataPathArgument.Load(parseResult);
            var generator = new InterfaceGenerator(deviceMetadata, namespaceOption.GetValue(parseResult, deviceMetadata));
            var implementation = generator.GenerateImplementation();
            GeneratorHelper.AssertNoGeneratorErrors(generator.Errors);
            GenerateCommand.WriteFileContents(outputPath.FullName, implementation);
        });
    }
}
