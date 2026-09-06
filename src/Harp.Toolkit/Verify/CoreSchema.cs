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

    /// <summary>
    /// Gets the core register metadata declared by the pinned generator version.
    /// </summary>
    public static DeviceMetadata Metadata => metadata.Value;

    static DeviceMetadata ReadMetadata()
    {
        using var stream = typeof(InterfaceGenerator).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"The core register metadata resource '{ResourceName}' was not found in the Harp.Generators assembly.");
        using var reader = new StreamReader(stream);
        return MetadataDeserializer.Instance.Deserialize<DeviceMetadata>(reader);
    }
}
