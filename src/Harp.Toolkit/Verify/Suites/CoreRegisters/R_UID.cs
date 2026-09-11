namespace Harp.Toolkit.Verify.Suites;

internal class R_UID : Suite
{
    internal const byte Address = 16;
    private const byte ExpectedLength = 16;
    public override string Description => "UID Register Tests";

    [HarpTest(Description = "Validates that UID register has exactly 16 bytes.", Prerelease = true)]
    public async Task<IResult> AssertLength(VerifyConnection device)
    {
        var value = await device.ReadByteArrayAsync(Address);
        return new AssertionResult(
            value.Length == ExpectedLength,
            x => x ?
                $"Length is {ExpectedLength} as expected." :
                $"Expected length of register to be {ExpectedLength}, got {value.Length} instead.");
    }

    [HarpTest(Description = "Checks if the register value is 0, indicating it is likely not used.", Prerelease = true)]
    public async Task<IResult> AssertReturnsZero(VerifyConnection device)
    {
        var value = await device.ReadByteArrayAsync(Address);
        string msg = value.All(x => x == 0) ? "Value of all bytes is 0. Register likely not being used." : $"Register returned a non-zero value: {BitConverter.ToString(value)}.";
        return new Result<byte[]>(
            value,
            Status.Passed,
            msg);
    }
}
