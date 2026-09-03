using System.Reflection;
using Bonsai.Harp;
using Harp.Generators;
using Harp.Toolkit.Benchmark;

namespace Harp.Toolkit.Benchmark.Suites;

/// <summary>
/// Validates a live device against the C# interface actually generated from device.yml,
/// not just the schema: for every register, it reads the live reply and parses it with
/// that register's own generated <c>GetPayload(HarpMessage)</c> (found via the generated
/// <see cref="Bonsai.Harp.Device.RegisterMap"/>, address -> register type, rather than
/// by guessing method names), catching real codegen/parser bugs (bad bit offsets, wrong
/// enum casts, mismatched payload struct fields) that a schema-only structural check
/// can't. It also cross-checks the WhoAmI/firmware/hardware version reported by the
/// device against device.yml. Core registers (WhoAmI, Heartbeat, version registers, etc.)
/// are covered automatically via the <see cref="Bonsai.Harp.Device.RegisterMap"/> base
/// chain that generated code links into, so this only needs to generate/compile
/// device-specific registers.
/// </summary>
/// <remarks>
/// Generating/compiling the interface and the identity check are always exactly one
/// test each, so they're plain <see cref="HarpTestAttribute"/> methods like every other
/// suite. Only the per-register checks are <see cref="DynamicTest"/>s, since the
/// register set is only known once device.yml has been parsed. The compile itself
/// happens eagerly in the constructor (not inside <see cref="GenerateAndCompileInterface"/>)
/// so the resulting register map is available up front to build those dynamic tests.
/// </remarks>
internal class DeviceInterfaceSuite : Suite
{
    private readonly DeviceMetadata? metadata;
    private readonly IReadOnlyDictionary<int, Type>? registerMap;
    private readonly Exception? compileError;
    private readonly IReadOnlyList<DynamicTest> dynamicTests;

    public DeviceInterfaceSuite(DeviceMetadata? metadata, string? rawYaml)
    {
        this.metadata = metadata;

        if (metadata is not null && rawYaml is not null)
        {
            try
            {
                registerMap = GeneratedInterfaceCompiler.Compile(metadata, rawYaml, $"Harp.{metadata.Device}");
            }
            catch (Exception ex)
            {
                while (ex is TargetInvocationException or TypeInitializationException && ex.InnerException is not null)
                    ex = ex.InnerException;
                compileError = ex;
            }
        }

        dynamicTests = registerMap is null
            ? new List<DynamicTest>()
            : BuildRegisterTests(registerMap);
    }

    protected override IReadOnlyList<DynamicTest> DynamicTests => dynamicTests;

    public override string Description =>
        "Validates the C# interface generated from device.yml by parsing live register replies with its own generated parsers, and cross-checks WhoAmI/firmware/hardware versions.";

    [HarpTest(Description = "Generates and compiles the C# interface from device.yml.")]
    public Task<IResult> GenerateAndCompileInterface(string portName)
    {
        IResult result = metadata is null
            ? new Result<bool>(false, Status.Skipped, "No device.yml provided (--device-yml).")
            : registerMap is not null
                ? new AssertionResult(true, $"Generated and compiled the interface with {registerMap.Count} registers.")
                : new ErrorResult(compileError!);
        return Task.FromResult(result);
    }

    [HarpTest(Description = "Compares the WhoAmI/firmware/hardware version reported by the device against device.yml.")]
    public async Task<IResult> DeviceIdentity(string portName)
    {
        if (metadata is null)
            return new Result<bool>(false, Status.Skipped, "No device.yml provided (--device-yml).");

        using var device = new AsyncDevice(portName);
        var mismatches = new List<string>();

        int whoAmI = await device.ReadWhoAmIAsync();
        if (whoAmI != metadata.WhoAmI)
            mismatches.Add($"WhoAmI: device={whoAmI}, device.yml={metadata.WhoAmI}");

        // HarpVersion.Satisfies treats a null Major/Minor on the argument as a wildcard,
        // so a device.yml that only pins a major version (minor left unset) is honored
        // automatically - no need to hand-roll that comparison.
        var firmware = await device.ReadFirmwareVersionAsync();
        if (metadata.FirmwareVersion is not null && !firmware.Satisfies(metadata.FirmwareVersion))
            mismatches.Add($"Firmware version: device={firmware}, device.yml={metadata.FirmwareVersion}");

        var hardware = await device.ReadHardwareVersionAsync();
        if (metadata.HardwareTargets is not null && !hardware.Satisfies(metadata.HardwareTargets))
            mismatches.Add($"Hardware version: device={hardware}, device.yml={metadata.HardwareTargets}");

        return new AssertionResult(
            mismatches.Count == 0,
            _ => mismatches.Count == 0
                ? $"WhoAmI={whoAmI}, Firmware={firmware}, Hardware={hardware} match device.yml."
                : string.Join("; ", mismatches));
    }

    private static IReadOnlyList<DynamicTest> BuildRegisterTests(IReadOnlyDictionary<int, Type> registerMap)
    {
        return registerMap
            .Where(entry => entry.Value.IsPublic)
            .OrderBy(entry => entry.Key)
            .Select(entry => new DynamicTest(
                entry.Value.Name,
                $"Reads register '{entry.Value.Name}' (address {entry.Key}) and parses the reply with its generated GetPayload parser.",
                (portName, cancellationToken) => CheckRegisterAsync(entry.Key, entry.Value, portName, cancellationToken)))
            .ToList();
    }

    private static async Task<IResult> CheckRegisterAsync(int address, Type registerType, string portName, CancellationToken cancellationToken)
    {
        const BindingFlags staticMembers = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        var getPayload = registerType.GetMethod("GetPayload", staticMembers, null, new[] { typeof(HarpMessage) }, null);
        if (getPayload is null)
            return new Result<bool>(false, Status.Skipped, $"Generated register '{registerType.Name}' has no GetPayload(HarpMessage) parser.");

        var payloadType = (PayloadType)registerType.GetField("RegisterType", staticMembers)!.GetValue(null)!;

        using var device = new AsyncDevice(portName);
        HarpMessage reply;
        try
        {
            reply = await device.CommandAsync(HarpCommand.Read(address, payloadType), cancellationToken);
        }
        catch (Exception ex)
        {
            return new ErrorResult(ex);
        }

        try
        {
            var value = getPayload.Invoke(null, new object?[] { reply });
            return new AssertionResult(true, $"Register '{registerType.Name}' parsed successfully: {value}.");
        }
        catch (TargetInvocationException ex)
        {
            // Unwrap so a real generated-parser bug (bad bit offset, wrong enum cast, ...)
            // is reported distinctly from the reflection-invocation exception itself.
            return new ErrorResult(ex.InnerException ?? ex);
        }
    }
}
