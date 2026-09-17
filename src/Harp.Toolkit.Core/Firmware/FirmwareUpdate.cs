using System.Diagnostics;
using Bonsai.Harp;

namespace Harp.Toolkit.Firmware;

/// <summary>
/// Provides asynchronous operations that any firmware update needs, whatever core the device runs.
/// </summary>
public static class FirmwareUpdate
{
    const int PollIntervalMilliseconds = 50;
    const int ProbeTimeoutMilliseconds = 500;

    /// <summary>
    /// Asynchronously waits until the Harp device on the specified port is ready to communicate.
    /// </summary>
    /// <param name="portName">The name of the serial port used to communicate with the Harp device.</param>
    /// <param name="timeout">
    /// The time to wait, in milliseconds, for the device to become ready. A negative timeout
    /// waits indefinitely.
    /// </param>
    /// <param name="cancellationToken">The token that cancels the wait.</param>
    /// <returns>
    /// The task object representing the asynchronous operation. The <see cref="Task{TResult}.Result"/>
    /// property is <b>true</b> if the device is ready; otherwise, <b>false</b>.
    /// </returns>
    /// <remarks>
    /// Wait only for a device that runs its application. A bootloader clears its fall-through
    /// timer on every byte it receives, so this wait holds a device in bootloader mode. Use
    /// <see cref="ATxmega.Bootloader.IsBootloaderAsync"/> for that case.
    /// </remarks>
    public static async Task<bool> WaitUntilReadyAsync(
        string portName,
        int timeout,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        do
        {
            if (await TryReadWhoAmIAsync(portName, cancellationToken))
            {
                return true;
            }

            await Task.Delay(PollIntervalMilliseconds, cancellationToken);
        }
        while (timeout < 0 || stopwatch.ElapsedMilliseconds < timeout);

        return false;
    }

    static async Task<bool> TryReadWhoAmIAsync(string portName, CancellationToken cancellationToken)
    {
        try
        {
            using var device = new AsyncDevice(portName);
            await device.ReadWhoAmIAsync(cancellationToken).WithTimeout(ProbeTimeoutMilliseconds);
            return true;
        }
        catch (Exception ex) when (ex is HarpException or TimeoutException or IOException or
                                         UnauthorizedAccessException or InvalidOperationException or
                                         ArgumentException)
        {
            return false;
        }
    }
}
