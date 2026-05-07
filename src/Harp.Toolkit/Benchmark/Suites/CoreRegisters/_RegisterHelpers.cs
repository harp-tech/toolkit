
using Bonsai.Harp;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Harp.Toolkit.Benchmark.Suites;

internal static class RegisterHelpers
{
    /// <summary>
    /// Opens a Device connection, writes messages via the synchronous transport,
    /// collects all received messages for the specified duration, then cleans up.
    /// </summary>
    public static async Task<IList<HarpMessage>> WriteToTransportAsync(
        string portName,
        IEnumerable<HarpMessage> messagesToWrite,
        TimeSpan listenDuration,
        Action<Bonsai.Harp.Device>? configureDevice = null)
    {
        var harpDevice = new Bonsai.Harp.Device { PortName = portName };
        configureDevice?.Invoke(harpDevice);

        var source = new Subject<HarpMessage>();
        var collected = new List<HarpMessage>();
        var tcs = new TaskCompletionSource<IList<HarpMessage>>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var subscription = harpDevice.Generate(source)
            .Subscribe(
                onNext: m => collected.Add(m),
                onError: ex => tcs.TrySetException(ex));

        // Small delay to let the transport connect
        await Task.Delay(200);

        foreach (var msg in messagesToWrite)
        {
            source.OnNext(msg);
        }

        await Task.Delay(listenDuration);

        source.OnCompleted();
        tcs.TrySetResult(collected);
        return await tcs.Task;
    }
    public static async Task<bool> IsWriteRejectedAsync(AsyncDevice device, HarpMessage write)
    {
        try
        {
            await device.CommandAsync(write);
            return false;
        }
        catch (HarpException)
        {
            return true;
        }
    }

    public static async Task<IResult> AssertReadableArrayAsync(AsyncDevice device, int address, int expectedLength, string registerName)
    {
        try
        {
            var value = await device.ReadByteArrayAsync(address);
            return new AssertionResult(
                value.Length == expectedLength,
                x => x
                    ? $"{registerName} is readable and has expected length ({expectedLength})."
                    : $"{registerName} returned {value.Length} bytes, expected {expectedLength}.");
        }
        catch (Exception ex)
        {
            return new ErrorResult(ex);
        }
    }

    public static async Task<IResult> AssertReadableByteAsync(AsyncDevice device, int address, string registerName)
    {
        try
        {
            await device.ReadByteAsync(address);
            return new AssertionResult(true, $"{registerName} is readable.");
        }
        catch (Exception ex)
        {
            return new ErrorResult(ex);
        }
    }
}
