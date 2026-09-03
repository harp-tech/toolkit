using System.Reflection;
using System.Text;
using Harp.Generators;
using Harp.Toolkit.Generate;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.Extensions.DependencyModel;

namespace Harp.Toolkit.Benchmark;

/// <summary>
/// Generates the C# interface for a device.yml (via <see cref="InterfaceGenerator"/>),
/// compiles it in-memory, and returns its register address-to-type map so callers can
/// invoke each register's own generated parser reflectively.
/// </summary>
internal static class GeneratedInterfaceCompiler
{
    public static IReadOnlyDictionary<int, Type> Compile(DeviceMetadata deviceOnlyMetadata, string rawDeviceYaml, string @namespace)
    {
        var generator = new InterfaceGenerator(deviceOnlyMetadata, @namespace);
        var implementation = generator.GenerateImplementation();
        if (!GeneratorHelper.AssertNoGeneratorErrors(generator.Errors))
            throw new InvalidOperationException("Interface generation from device.yml completed with errors.");

        var syntaxTree = CSharpSyntaxTree.ParseText(implementation.Device);
        var compilation = CSharpCompilation.Create(
            $"HarpGeneratedInterface_{@namespace}",
            new[] { syntaxTree },
            GetMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

        // The generated Device class's static constructor reads device.yml back from
        // an embedded "{Namespace}.device.yml" manifest resource (e.g. to expose it via
        // the Metadata property) - without it, merely accessing RegisterMap throws.
        var rawYamlBytes = Encoding.UTF8.GetBytes(rawDeviceYaml);
        var deviceYamlResource = new ResourceDescription(
            $"{@namespace}.device.yml",
            () => new MemoryStream(rawYamlBytes),
            isPublic: true);

        using var peStream = new MemoryStream();
        var result = compilation.Emit(peStream, manifestResources: new[] { deviceYamlResource });
        if (!result.Success)
        {
            var errors = string.Join(Environment.NewLine, result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            throw new InvalidOperationException($"Failed to compile the interface generated from device.yml:{Environment.NewLine}{errors}");
        }

        var assembly = Assembly.Load(peStream.ToArray());
        var deviceType = assembly.GetType($"{@namespace}.Device")
            ?? throw new InvalidOperationException($"Compiled assembly does not contain type '{@namespace}.Device'.");
        var registerMapProperty = deviceType.GetProperty("RegisterMap", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"'{@namespace}.Device' does not expose a static RegisterMap property.");

        return (IReadOnlyDictionary<int, Type>)registerMapProperty.GetValue(null)!;
    }

    // Reuses this project's existing PreserveCompilationContext setup (already required
    // for RazorLight's own runtime compilation) to resolve the full reference-assembly
    // closure, including Bonsai.Harp/Bonsai.Core, which the generated code depends on.
    private static IReadOnlyList<MetadataReference> GetMetadataReferences()
    {
        var context = DependencyContext.Default
            ?? throw new InvalidOperationException("No DependencyContext available for compiling the generated interface.");

        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var library in context.CompileLibraries)
        {
            foreach (var path in library.ResolveReferencePaths())
            {
                paths.Add(path);
            }
        }

        return paths.Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToList();
    }
}
