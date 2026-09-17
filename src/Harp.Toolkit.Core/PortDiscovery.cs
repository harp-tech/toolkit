using System.IO.Ports;

namespace Harp.Toolkit;

/// <summary>
/// Provides methods to list the serial ports that can connect to a Harp device.
/// </summary>
public static class PortDiscovery
{
    /// <summary>
    /// Returns the name of each serial port that can connect to a Harp device.
    /// </summary>
    /// <returns>An array of serial port names.</returns>
    /// <remarks>
    /// macOS lists each serial device twice, once as a tty device and once as a callout device.
    /// Only the callout device opens without a carrier signal, so this method returns the callout
    /// devices alone. It also drops the Bluetooth port, which macOS lists even when nothing is
    /// paired. Windows and Linux need no filter.
    /// </remarks>
    public static string[] GetCandidatePortNames()
    {
        var portNames = SerialPort.GetPortNames();
        return OperatingSystem.IsMacOS()
            ? Array.FindAll(portNames, IsCalloutPort)
            : portNames;
    }

    static bool IsCalloutPort(string portName)
    {
        var fileName = Path.GetFileName(portName);
        return fileName.StartsWith("cu.", StringComparison.Ordinal)
            && !fileName.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase);
    }
}
