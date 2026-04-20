
using Bonsai.Harp;
namespace Harp.Toolkit.Benchmark.Suites;

internal class R_ASSEMBLY_VERSION : Suite
{
    public override string Description => "AssemblyVersion Register Tests";

    [HarpTest(Description = "Validates the deprecated register AssemblyVersion returns 0x00.")]
    public async Task<IResult> AssertReturnsZero(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            int value = await device.ReadAssemblyVersionAsync();
            bool isZero = value == 0x00;
            return new AssertionResult(
                isZero,
                x => x ?
                    $"AssemblyVersion register correctly returned 0x00." :
                    $"AssemblyVersion register returned a non-zero value (0x{value:X2})");
        }
    }
}
