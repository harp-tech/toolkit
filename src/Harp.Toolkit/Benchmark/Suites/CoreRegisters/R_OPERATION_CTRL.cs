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
        ushort whoAmI = 0;

        try
        {
            using (var device = new AsyncDevice(portName))
            {
                originalOpCtrl = await device.ReadByteAsync(address);
                whoAmI = await device.ReadUInt16Async(WhoAmI.Address);
            }
            await Task.Delay(500); // The previous one needs some time to disconnect

            var harpDevice = new Bonsai.Harp.Device(whoAmI) { PortName = portName };
            var messages = await RegisterHelpers.WriteToTransportAsync(
                portName,
                new[] { HarpMessage.FromByte(address, MessageType.Write, 0xE5) },
                TimeSpan.FromSeconds(2.0));

            bool received = messages.Any(m => m.Address == 18 && m.MessageType == MessageType.Event);

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
            await Task.Delay(200); // Wait for port to be released before reopening
            using (var device = new AsyncDevice(portName))
            {
                await device.CommandAsync(HarpMessage.FromByte(address, MessageType.Write, originalOpCtrl));
            }
        }
    }

    [HarpTest(Description = "Validates that HEARTBEAT_EN (bit 2) takes precedence over ALIVE_EN (bit 7): when both are set, R_HEARTBEAT events are emitted and R_TIMESTAMP_SECOND events are not.")]
    public async Task<IResult> HeartbeatEnPrecedenceOverAliveEn(string portName)
    {
        byte originalOpCtrl = 0;
        ushort whoAmI = 0;

        try
        {
            using (var device = new AsyncDevice(portName))
            {
                originalOpCtrl = await device.ReadByteAsync(address);
                whoAmI = await device.ReadUInt16Async(WhoAmI.Address);
            }
            await Task.Delay(500);

            var harpDevice = new Bonsai.Harp.Device(whoAmI) { PortName = portName };
            // Set both ALIVE_EN (bit 7) and HEARTBEAT_EN (bit 2) with Active mode (bit 0)
            var messages = await RegisterHelpers.WriteToTransportAsync(
                portName,
                new[] { HarpMessage.FromByte(address, MessageType.Write, 0x85) },
                TimeSpan.FromSeconds(2.0));

            bool receivedHeartbeat = messages.Any(m => m.Address == 18 && m.MessageType == MessageType.Event);
            bool receivedTimestamp = messages.Any(m => m.Address == 8 && m.MessageType == MessageType.Event);

            if (!receivedHeartbeat)
                return new AssertionResult(false, "HeartbeatEnPrecedenceOverAliveEn: no R_HEARTBEAT event received within 2s (expected HEARTBEAT_EN to take precedence).");
            if (receivedTimestamp)
                return new AssertionResult(false, "HeartbeatEnPrecedenceOverAliveEn: R_TIMESTAMP_SECOND event received when HEARTBEAT_EN should suppress it.");

            return new AssertionResult(true, "HeartbeatEnPrecedenceOverAliveEn: R_HEARTBEAT events received and R_TIMESTAMP_SECOND correctly suppressed.");
        }
        catch (Exception ex)
        {
            return new ErrorResult(ex);
        }
        finally
        {
            await Task.Delay(200);
            using (var device = new AsyncDevice(portName))
            {
                await device.CommandAsync(HarpMessage.FromByte(address, MessageType.Write, originalOpCtrl));
            }
        }
    }

    [HarpTest(Description = "Validates that ALIVE_EN (deprecated, bit 7) causes R_TIMESTAMP_SECOND events to be emitted when HEARTBEAT_EN is not set.")]
    public async Task<IResult> AliveEnEmitsTimestampEvents(string portName)
    {
        byte originalOpCtrl = 0;
        ushort whoAmI = 0;

        try
        {
            using (var device = new AsyncDevice(portName))
            {
                originalOpCtrl = await device.ReadByteAsync(address);
                whoAmI = await device.ReadUInt16Async(WhoAmI.Address);
            }
            await Task.Delay(500);

            var harpDevice = new Bonsai.Harp.Device(whoAmI) { PortName = portName };
            // Set only ALIVE_EN (bit 7) with Active mode (bit 0); HEARTBEAT_EN (bit 2) is cleared
            var messages = await RegisterHelpers.WriteToTransportAsync(
                portName,
                new[] { HarpMessage.FromByte(address, MessageType.Write, 0x81) },
                TimeSpan.FromSeconds(2.0));

            bool receivedTimestamp = messages.Any(m => m.Address == 8 && m.MessageType == MessageType.Event);

            if (!receivedTimestamp)
                return new Result<bool>(false, Status.Skipped, "AliveEnEmitsTimestampEvents: ALIVE_EN is deprecated and R_TIMESTAMP_SECOND events were not emitted.");

            return new AssertionResult(true, "AliveEnEmitsTimestampEvents: R_TIMESTAMP_SECOND event received within 2s.");
        }
        catch (Exception ex)
        {
            return new ErrorResult(ex);
        }
        finally
        {
            await Task.Delay(200);
            using (var device = new AsyncDevice(portName))
            {
                await device.CommandAsync(HarpMessage.FromByte(address, MessageType.Write, originalOpCtrl));
            }
        }
    }

    [HarpTest(Description = "Validates that the DUMP bit triggers a burst of all core register reads after an OpCtrl write.")]
    public async Task<IResult> RegisterDump(string portName)
    {
        byte originalOpCtrl = 0;
        ushort whoAmI = 0;

        try
        {
            // Read original state before modifying
            using (var device = new AsyncDevice(portName))
            {
                originalOpCtrl = await device.ReadByteAsync(address);
                whoAmI = await device.ReadUInt16Async(WhoAmI.Address);
            }

            var harpDevice = new Bonsai.Harp.Device(whoAmI) { PortName = portName };
            var messages = await RegisterHelpers.WriteToTransportAsync(
                portName,
                new[] { HarpMessage.FromByte(address, MessageType.Write, (byte)(originalOpCtrl | 0x08)) },
                TimeSpan.FromSeconds(1));

            var opRegWriteResponse = messages.FirstOrDefault(m => m.Address == address && m.MessageType == MessageType.Write);
            if (opRegWriteResponse == null)
            {
                return new AssertionResult(false, "No response received for OpCtrl write.");
            }
            var coreReads = messages
                .Select((m, i) => (msg: m, idx: i))
                .Where(x => x.msg.Address <= 32 && x.msg.MessageType == MessageType.Read)
                .ToList();
            var uniqueCoreAddresses = coreReads.Select(x => x.msg.Address).Distinct().ToHashSet();
            var missing = Enumerable.Range(0, 18).Where(a => !uniqueCoreAddresses.Contains(a)).ToList();
            if (missing.Count > 0)
                return new AssertionResult(false,
                    $"Missing Read replies for {missing.Count} core address(es): {string.Join(", ", missing.Select(a => $"0x{a:X2}"))}.");

            return new AssertionResult(true, "All core register reads received after OpCtrl write.");
        }
        catch (Exception ex)
        {
            return new ErrorResult(ex);
        }
        finally
        {
            await Task.Delay(200);
            // Ensure we restore original state even though DUMP is transient
            using (var device = new AsyncDevice(portName))
            {
                await device.CommandAsync(HarpMessage.FromByte(address, MessageType.Write, originalOpCtrl));
            }
        }
    }

    private static async Task<IResult> TestOptionalBitAsync(AsyncDevice device, string bitName, byte bitMask)
    {
        var original = await device.ReadByteAsync(address);
        byte toggled = (byte)(original ^ bitMask);

        try
        {
            try
            {
                await device.CommandAsync(HarpMessage.FromByte(address, MessageType.Write, toggled));
            }
            catch (HarpException)
            {
                return new Result<bool>(false, Status.Skipped,
                    $"{bitName} is optional/deprecated and not supported by this device.");
            }

            var readBack = await device.ReadByteAsync(address);
            bool bitChanged = (readBack & bitMask) == (toggled & bitMask);

            return new AssertionResult(
                bitChanged,
                x => x
                    ? $"{bitName}: bit correctly toggled."
                    : $"{bitName}: bit did not change after write (expected {(toggled & bitMask) != 0}, got {(readBack & bitMask) != 0}).");
        }
        finally
        {
            try
            {
                await device.CommandAsync(HarpMessage.FromByte(address, MessageType.Write, original));
            }
            catch
            {
            }
        }
    }
}
