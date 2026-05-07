using Bonsai.Harp;
using System.Reactive.Linq;
using System.Collections.Concurrent;

namespace Harp.Toolkit.Benchmark.Suites;

internal class R_OPERATION_CTRL : Suite
{
    private const byte address = 0x0A;
    public override string Description => "Operation Control Register Tests";

    [HarpTest(Description = "Validates that OP_MODE bits can be round-tripped between Standby (0) and Active (1).")]
    public async Task<IResult> OpModeRoundTrip(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            var original = await device.ReadByteAsync(address);
            byte currentMode = (byte)(original & 0x03);
            byte newMode = currentMode == 0x01 ? (byte)0x00 : (byte)0x01;
            byte newValue = (byte)((original & ~0x03) | newMode);

            try
            {
                await device.CommandAsync(HarpMessage.FromByte(address, MessageType.Write, newValue));
                var readBack = await device.ReadByteAsync(address);
                byte readMode = (byte)(readBack & 0x03);

                return new AssertionResult(
                    readMode == newMode,
                    x => x
                        ? $"OpModeRoundTrip: OP_MODE correctly round-tripped to {newMode}."
                        : $"OpModeRoundTrip: wrote OP_MODE={newMode}, read back OP_MODE={readMode}.");
            }
            finally
            {
                // Always restore original state
                try
                {
                    await device.CommandAsync(HarpMessage.FromByte(address, MessageType.Write, original));
                }
                catch
                {
                    // Ignore errors during restoration
                }
            }
        }
    }

    [HarpTest(Description = "Validates that ALIVE_EN (deprecated, bit 7) can be toggled, or reports as unsupported.")]
    public async Task<IResult> AliveEnWritable(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            return await TestOptionalBitAsync(device, "AliveEn", 0x80);
        }
    }

    [HarpTest(Description = "Validates that OPLED_EN (optional, bit 6) can be toggled, or reports as unsupported.")]
    public async Task<IResult> OpLedEnWritable(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            return await TestOptionalBitAsync(device, "OpLedEn", 0x40);
        }
    }

    [HarpTest(Description = "Validates that VISUAL_EN (optional, bit 5) can be toggled, or reports as unsupported.")]
    public async Task<IResult> VisualEnWritable(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            return await TestOptionalBitAsync(device, "VisualEn", 0x20);
        }
    }

    [HarpTest(Description = "Validates that enabling HEARTBEAT_EN causes the device to emit R_HEARTBEAT events.")]
    public async Task<IResult> HeartbeatEnEmitsEvents(string portName)
    {
        byte originalOpCtrl = 0;
        IDisposable? subscription = null;

        try
        {
            // Read original state before modifying
            using (var device = new AsyncDevice(portName))
            {
                originalOpCtrl = await device.ReadByteAsync(address);
            }

            var harpDevice = new Bonsai.Harp.Device { PortName = portName, Heartbeat = EnableFlag.Enabled };
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            cts.Token.Register(() => tcs.TrySetResult(false));

            subscription = harpDevice.Generate()
                .Where(m => m.Address == 0x12 && m.MessageType == MessageType.Event)
                .Take(1)
                .Subscribe(
                    _ => tcs.TrySetResult(true),
                    ex => tcs.TrySetException(ex));

            bool received = await tcs.Task;

            return new AssertionResult(
                received,
                x => x
                    ? "HeartbeatEnEmitsEvents: heartbeat event received within 2s."
                    : "HeartbeatEnEmitsEvents: no heartbeat event received within 2s.");
        }
        catch (Exception ex)
        {
            return new ErrorResult(ex);
        }
        finally
        {
            subscription?.Dispose();
            await Task.Delay(200); // Fudge delay to ensure port is released

            // Always restore original Operation Control state
            using (var device = new AsyncDevice(portName))
            {
                await device.CommandAsync(HarpMessage.FromByte(address, MessageType.Write, originalOpCtrl));
            }

        }
    }

    [HarpTest(Description = "Validates that the DUMP bit triggers a burst of all core register reads after an OpCtrl write.")]
    public async Task<IResult> DumpEmitsRegisterBurst(string portName)
    {
        byte originalOpCtrl = 0;
        var messages = new ConcurrentQueue<HarpMessage>();
        IDisposable? subscription = null;

        try
        {
            // Read original state before modifying
            using (var device = new AsyncDevice(portName))
            {
                originalOpCtrl = await device.ReadByteAsync(address);
            }

            var harpDevice = new Bonsai.Harp.Device { PortName = portName, DumpRegisters = true };
            subscription = harpDevice.Generate()
                .Subscribe(m => messages.Enqueue(m));

            await Task.Delay(1000);

            var snapshot = messages.ToList();

            int opCtrlWriteIdx = -1;
            for (int i = 0; i < snapshot.Count; i++)
            {
                if (snapshot[i].Address == 0x0A && snapshot[i].MessageType == MessageType.Write)
                {
                    opCtrlWriteIdx = i;
                    break;
                }
            }

            if (opCtrlWriteIdx < 0)
                return new AssertionResult(false, "DumpEmitsRegisterBurst: no Write reply at OpCtrl (0x0A) found.");

            var coreReads = snapshot
                .Select((m, i) => (msg: m, idx: i))
                .Where(x => x.msg.Address <= 0x13 && x.msg.MessageType == MessageType.Read)
                .ToList();

            bool writeBeforeAllReads = coreReads.All(x => opCtrlWriteIdx < x.idx);
            if (!writeBeforeAllReads)
                return new AssertionResult(false, "DumpEmitsRegisterBurst: OpCtrl Write reply did not precede all core Read replies.");

            var presentAddresses = coreReads.Select(x => (int)x.msg.Address).Distinct().ToHashSet();
            var missing = Enumerable.Range(0, 0x14).Where(a => !presentAddresses.Contains(a)).ToList();
            if (missing.Count > 0)
                return new AssertionResult(false,
                    $"DumpEmitsRegisterBurst: missing Read replies for {missing.Count} core address(es): {string.Join(", ", missing.Select(a => $"0x{a:X2}"))}.");

            return new AssertionResult(true, "DumpEmitsRegisterBurst: all 20 core register reads received after OpCtrl write.");
        }
        catch (Exception ex)
        {
            return new ErrorResult(ex);
        }
        finally
        {
            subscription?.Dispose();
            await Task.Delay(200);

            // Ensure we restore original state even though DUMP is transient
            using (var device = new AsyncDevice(portName))
            {
                await device.CommandAsync(HarpMessage.FromByte(address, MessageType.Write, originalOpCtrl));
            }
        }
    }
}
