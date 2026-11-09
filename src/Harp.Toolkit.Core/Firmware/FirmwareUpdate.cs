using System.Diagnostics;

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
    /// <param name="cancellationToken">
    /// The token to monitor for cancellation requests. The default value is
    /// <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// The task object representing the asynchronous operation. The <see cref="Task{TResult}.Result"/>
    /// property is <see langword="true"/> if the device is ready; otherwise, <see langword="false"/>.
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
            var probe = await DeviceProbe.ProbeAsync(portName, ProbeTimeoutMilliseconds, cancellationToken);
            if (probe.Status == DeviceProbeStatus.Responding)
            {
                return true;
            }

            await Task.Delay(PollIntervalMilliseconds, cancellationToken);
        }
        while (timeout < 0 || stopwatch.ElapsedMilliseconds < timeout);

        return false;
    }
}
