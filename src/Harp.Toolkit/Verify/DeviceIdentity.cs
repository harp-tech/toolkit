using Bonsai.Harp;

namespace Harp.Toolkit.Verify;

internal readonly record struct DeviceIdentity(
    int WhoAmI,
    string? Name,
    HarpVersion? HardwareVersion,
    HarpVersion? FirmwareVersion);
