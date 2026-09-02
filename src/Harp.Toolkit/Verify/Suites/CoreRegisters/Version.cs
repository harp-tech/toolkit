using System.Text;
using Bonsai.Harp;

namespace Harp.Toolkit.Verify.Suites;

/// <summary>
/// Provides methods for manipulating messages from the Version register.
/// </summary>
internal partial class Version
{
    /// <summary>
    /// Represents the address of the <see cref="Version"/> register. This field is constant.
    /// </summary>
    public const int Address = 19;

    /// <summary>
    /// Represents the payload type of the <see cref="Version"/> register. This field is constant.
    /// </summary>
    public const PayloadType RegisterType = PayloadType.U8;

    /// <summary>
    /// Represents the length of the <see cref="Version"/> register. This field is constant.
    /// </summary>
    public const int RegisterLength = 32;

    static VersionPayload ParsePayload(byte[] payload)
    {
        VersionPayload result;
        result.ProtocolVersion = ReadSemanticVersion(new ArraySegment<byte>(payload, 0, 3));
        result.FirmwareVersion = ReadSemanticVersion(new ArraySegment<byte>(payload, 3, 3));
        result.HardwareVersion = ReadSemanticVersion(new ArraySegment<byte>(payload, 6, 3));
        result.CoreId = ReadUtf8String(new ArraySegment<byte>(payload, 9, 3));
        result.InterfaceHash = GetSubArray(payload, 12, 20);
        return result;
    }

    /// <summary>
    /// Returns the payload data for <see cref="Version"/> register messages.
    /// </summary>
    /// <param name="message">A <see cref="HarpMessage"/> object representing the register message.</param>
    /// <returns>A value representing the message payload.</returns>
    public static VersionPayload GetPayload(HarpMessage message)
    {
        return ParsePayload(message.GetPayloadArray<byte>());
    }

    static SemanticVersion ReadSemanticVersion(ArraySegment<byte> segment)
    {
        var array = segment.Array!;
        return new SemanticVersion(
            array[segment.Offset],
            array[segment.Offset + 1],
            array[segment.Offset + 2]);
    }

    static string ReadUtf8String(ArraySegment<byte> segment)
    {
        var array = segment.Array!;
        var count = Array.IndexOf(array, (byte)0, segment.Offset, segment.Count) - segment.Offset;
        return Encoding.UTF8.GetString(array, segment.Offset, count < 0 ? segment.Count : count);
    }

    static byte[] GetSubArray(byte[] array, int offset, int count)
    {
        var result = new byte[count];
        Array.Copy(array, offset, result, 0, count);
        return result;
    }
}

/// <summary>
/// Represents the payload of the Version register.
/// </summary>
internal struct VersionPayload
{
    /// <summary>
    /// The semantic version of the Harp protocol implemented by the device.
    /// </summary>
    public SemanticVersion ProtocolVersion;

    /// <summary>
    /// The semantic version of the device firmware application.
    /// </summary>
    public SemanticVersion FirmwareVersion;

    /// <summary>
    /// The semantic version of the device hardware.
    /// </summary>
    public SemanticVersion HardwareVersion;

    /// <summary>
    /// The three-character code of the Harp microcontroller core targeted by the device
    /// firmware.
    /// </summary>
    public string CoreId;

    /// <summary>
    /// The SHA-1 hash value of the device interface schema file, all zeros when the device
    /// declares no schema for the controller to validate against.
    /// </summary>
    public byte[] InterfaceHash;

    /// <summary>
    /// Returns a <see cref="string"/> that represents the payload of the Version register.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that represents the payload of the Version register.
    /// </returns>
    public override readonly string ToString()
    {
        return "VersionPayload { " +
            "ProtocolVersion = " + ProtocolVersion + ", " +
            "FirmwareVersion = " + FirmwareVersion + ", " +
            "HardwareVersion = " + HardwareVersion + ", " +
            "CoreId = " + CoreId + ", " +
            "InterfaceHash = " + (InterfaceHash is null ? string.Empty : Convert.ToHexString(InterfaceHash)) + " " +
        "}";
    }
}

/// <summary>
/// Represents the semantic version of a device component reported by the Version register.
/// </summary>
internal readonly struct SemanticVersion
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SemanticVersion"/> structure.
    /// </summary>
    /// <param name="major">The major version number.</param>
    /// <param name="minor">The minor version number.</param>
    /// <param name="patch">The patch version number.</param>
    public SemanticVersion(byte major, byte minor, byte patch)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
    }

    /// <summary>
    /// The major version number.
    /// </summary>
    public byte Major { get; }

    /// <summary>
    /// The minor version number.
    /// </summary>
    public byte Minor { get; }

    /// <summary>
    /// The patch version number.
    /// </summary>
    public byte Patch { get; }

    /// <summary>
    /// Returns a <see cref="string"/> that represents the semantic version.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> containing the major, minor and patch numbers separated by dots.
    /// </returns>
    public override string ToString()
    {
        return $"{Major}.{Minor}.{Patch}";
    }
}
