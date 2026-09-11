using System.Reflection;
using Harp.Generators;

namespace Harp.Toolkit.Verify;

/// <summary>
/// Provides the core register metadata embedded in the Harp.Generators assembly, which declares
/// the register set and payload types a conformant device must implement.
/// </summary>
internal static class CoreSchema
{
    const string ResourceName = "Harp.Generators.core.yml";

    static readonly Lazy<DeviceMetadata> metadata = new(ReadMetadata);
    static readonly Lazy<string> version = new(ReadVersion);

    /// <summary>
    /// Gets the core register metadata declared by the pinned generator version.
    /// </summary>
    public static DeviceMetadata Metadata => metadata.Value;

    /// <summary>
    /// Gets the version of the generator package supplying the core register metadata. This
    /// version fully determines the register set, since the metadata ships inside the package.
    /// </summary>
    public static string Version => version.Value;

    static string ReadVersion()
    {
        var assembly = typeof(InterfaceGenerator).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var text = informational ?? assembly.GetName().Version?.ToString();
        if (string.IsNullOrEmpty(text))
            return "unknown";

        var metadataSeparator = text.IndexOf('+');
        return metadataSeparator < 0 ? text : text[..metadataSeparator];
    }

    static DeviceMetadata ReadMetadata()
    {
        using var stream = typeof(InterfaceGenerator).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"The core register metadata resource '{ResourceName}' was not found in the Harp.Generators assembly.");
        using var reader = new StreamReader(stream);
        return MetadataDeserializer.Instance.Deserialize<DeviceMetadata>(reader);
    }
}
