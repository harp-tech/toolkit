
using Bonsai.Harp;
namespace Harp.Toolkit.Benchmark.Suites;

internal class R_SERIAL_NUMBER : Suite
{
    public override string Description => "Serial Number Register Tests";

    [HarpTest(Description = "Validates the contents of the register match the lower two bytes of R_UID")]
    public async Task<IResult> AssertConsitentWithUid(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            var uidValue = await device.ReadByteArrayAsync(0x10);
            if (uidValue.Length < 2)
                throw new ArgumentException($"Expected UID register contents to be at least 2 bytes. Got {uidValue.Length}");
            var twoFirstBytes = BitConverter.ToInt16(uidValue, 0);

            var serialNumberValue = await device.ReadSerialNumberAsync();

            return new AssertionResult(
                twoFirstBytes == serialNumberValue,
                x => x ?
                    $"SerialNumber register contents are consistent with UID register." :
                    $"SerialNumber register content (0x{serialNumberValue:X4}) does not match the first two bytes of UID register (0x{twoFirstBytes:X4}).");
        }
    }
}
