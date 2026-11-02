using System.CommandLine;
using Harp.Generators;

namespace Harp.Toolkit.Generate;

class GenerateDocfxDocumentationCommand : Command
{
    public GenerateDocfxDocumentationCommand()
        : base("docfx", "Generate docfx reference pages and an index for the device registers.")
    {
        MetadataPathArgument metadataPathArgument = new();
        NamespaceOption namespaceOption = new()
        {
            Description = "Namespace used to link the pages to the .NET interface. The default is Harp.DeviceName."
        };
        OutputPathOption outputPathOption = new()
        {
            Required = true,
            DefaultValueFactory = null
        };

        Arguments.Add(metadataPathArgument);
        Options.Add(namespaceOption);
        Options.Add(outputPathOption);

        SetAction(parseResult =>
        {
            var outputPath = parseResult.GetRequiredValue(outputPathOption);

            var deviceMetadata = metadataPathArgument.Load(parseResult);
            var ns = namespaceOption.GetValue(parseResult, deviceMetadata);
            var generator = new DocumentationGenerator(deviceMetadata, ns);
            var reference = new DocfxRegisterReference(generator.GenerateImplementation(), ns);
            GenerateCommand.WriteFileContents(outputPath.FullName, reference.Pages);
        });
    }
}
