using Bonsai.Harp;

namespace Harp.Toolkit.Verify.Suites;

internal static class RegisterHelpers
{
    public static async Task<bool> IsWriteRejectedAsync(VerifyConnection device, HarpMessage write)
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

    public static async Task<IResult> AssertReadableArrayAsync(VerifyConnection device, int address, int expectedLength, string registerName)
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

    public static async Task<IResult> AssertReadableAsync<T>(Func<int, Task<T>> readFunc, int address, string registerName)
    {
        try
        {
            await readFunc(address);
            return new AssertionResult(true, $"{registerName} is readable.");
        }
        catch (Exception ex)
        {
            return new ErrorResult(ex);
        }
    }
}
