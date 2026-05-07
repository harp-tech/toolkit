
using Bonsai.Harp;

namespace Harp.Toolkit.Benchmark.Suites;

internal static class RegisterHelpers
{
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
