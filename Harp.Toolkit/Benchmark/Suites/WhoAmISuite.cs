
using Bonsai.Harp;
namespace Harp.Toolkit;

public class WhoAmISuite : Suite
{
    public override string Description => "WhoAmI Register Tests";

    [HarpTest(Description = "Validates that the WhoAmI register exists and contains a value.")]
    public async Task<IResult> CheckWhoAmI(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            int value = await device.ReadWhoAmIAsync();
            return new Result<int>(
                value,
                (v) => v > 0 && v < 9999,
                (v, success) => success ? $"WhoAmI register contains valid value: {v}." : $"WhoAmI register contains invalid value: {v}.");
        }
    }
}
