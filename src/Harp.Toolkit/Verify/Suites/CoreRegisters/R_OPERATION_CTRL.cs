using Bonsai.Harp;

namespace Harp.Toolkit.Verify.Suites;

internal class R_OPERATION_CTRL : Suite
{
    public override string Description => "Operation Control Register Tests";

    [HarpTest(Description = "Validates that OP_MODE bits can be round-tripped between Standby (0) and Active (1).")]
    public async Task<IResult> OpModeRoundTrip(VerifyConnection device)
    {
        var original = await device.ReadByteAsync(OperationControl.Address);
        byte currentMode = (byte)(original & 0x03);
        byte newMode = currentMode == 0x01 ? (byte)0x00 : (byte)0x01;
        byte newValue = (byte)((original & ~0x03) | newMode);

        try
        {
            await device.CommandAsync(HarpMessage.FromByte(OperationControl.Address, MessageType.Write, newValue));
            var readBack = await device.ReadByteAsync(OperationControl.Address);
            byte readMode = (byte)(readBack & 0x03);

            return new AssertionResult(
                readMode == newMode,
                x => x
                    ? $"OpModeRoundTrip: OP_MODE correctly round-tripped to {newMode}."
                    : $"OpModeRoundTrip: wrote OP_MODE={newMode}, read back OP_MODE={readMode}.");
        }
        finally
        {
            await RestoreOperationControlAsync(device, original);
        }
    }

    [HarpTest(Description = "Validates that ALIVE_EN (deprecated, bit 7) can be toggled, or reports as unsupported.")]
    public async Task<IResult> AliveEnWritable(VerifyConnection device)
    {
        return await TestOptionalBitAsync(device, "AliveEn", 0x80);
    }

    [HarpTest(Description = "Validates that OPLED_EN (optional, bit 6) can be toggled, or reports as unsupported.")]
    public async Task<IResult> OpLedEnWritable(VerifyConnection device)
    {
        return await TestOptionalBitAsync(device, "OpLedEn", 0x40);
    }

    [HarpTest(Description = "Validates that VISUAL_EN (optional, bit 5) can be toggled, or reports as unsupported.")]
    public async Task<IResult> VisualEnWritable(VerifyConnection device)
    {
        return await TestOptionalBitAsync(device, "VisualEn", 0x20);
    }

    [HarpTest(Description = "Validates that enabling HEARTBEAT_EN causes the device to emit R_HEARTBEAT events.")]
    public async Task<IResult> HeartbeatEnEmitsEvents(VerifyConnection device)
    {
        byte? originalOpCtrl = null;

        try
        {
            originalOpCtrl = await device.ReadByteAsync(OperationControl.Address);
            var messages = await device.WriteAndCollectAsync(
                new[] { HarpMessage.FromByte(OperationControl.Address, MessageType.Write, 0x05) },
                TimeSpan.FromSeconds(2.0));

            bool received = messages.Any(m => m.Address == R_HEARTBEAT.Address && m.MessageType == MessageType.Event);

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
            await RestoreOperationControlAsync(device, originalOpCtrl);
        }
    }

    [HarpTest(Description = "Validates that HEARTBEAT_EN (bit 2) takes precedence over ALIVE_EN (bit 7): when both are set, R_HEARTBEAT events are emitted and R_TIMESTAMP_SECOND events are not.")]
    public async Task<IResult> HeartbeatEnPrecedenceOverAliveEn(VerifyConnection device)
    {
        byte? originalOpCtrl = null;

        try
        {
            originalOpCtrl = await device.ReadByteAsync(OperationControl.Address);
            // Set both ALIVE_EN (bit 7) and HEARTBEAT_EN (bit 2) with Active mode (bit 0)
            var messages = await device.WriteAndCollectAsync(
                new[] { HarpMessage.FromByte(OperationControl.Address, MessageType.Write, 0x85) },
                TimeSpan.FromSeconds(2.0));

            bool receivedHeartbeat = messages.Any(m => m.Address == R_HEARTBEAT.Address && m.MessageType == MessageType.Event);
            bool receivedTimestamp = messages.Any(m => m.Address == TimestampSeconds.Address && m.MessageType == MessageType.Event);

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
            await RestoreOperationControlAsync(device, originalOpCtrl);
        }
    }

    [HarpTest(Description = "Validates that ALIVE_EN (deprecated, bit 7) causes R_TIMESTAMP_SECOND events to be emitted when HEARTBEAT_EN is not set.")]
    public async Task<IResult> AliveEnEmitsTimestampEvents(VerifyConnection device)
    {
        byte? originalOpCtrl = null;

        try
        {
            originalOpCtrl = await device.ReadByteAsync(OperationControl.Address);
            // Set only ALIVE_EN (bit 7) with Active mode (bit 0); HEARTBEAT_EN (bit 2) is cleared
            var messages = await device.WriteAndCollectAsync(
                new[] { HarpMessage.FromByte(OperationControl.Address, MessageType.Write, 0x81) },
                TimeSpan.FromSeconds(2.0));

            bool receivedTimestamp = messages.Any(m => m.Address == TimestampSeconds.Address && m.MessageType == MessageType.Event);

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
            await RestoreOperationControlAsync(device, originalOpCtrl);
        }
    }

    [HarpTest(Description = "Validates that the DUMP bit triggers a burst of all core register reads after an OpCtrl write.")]
    public async Task<IResult> RegisterDump(VerifyConnection device)
    {
        byte? originalOpCtrl = null;

        try
        {
            originalOpCtrl = await device.ReadByteAsync(OperationControl.Address);
            var messages = await device.WriteAndCollectAsync(
                new[] { HarpMessage.FromByte(OperationControl.Address, MessageType.Write, (byte)(originalOpCtrl.GetValueOrDefault() | 0x08)) },
                TimeSpan.FromSeconds(1));

            var opRegWriteResponse = messages.FirstOrDefault(m => m.Address == OperationControl.Address && m.MessageType == MessageType.Write);
            if (opRegWriteResponse == null)
            {
                return new AssertionResult(false, "No response received for OpCtrl write.");
            }
            var coreReads = messages
                .Select((m, i) => (msg: m, idx: i))
                .Where(x => x.msg.Address < 32 && x.msg.MessageType == MessageType.Read)
                .ToList();
            var uniqueCoreAddresses = coreReads.Select(x => x.msg.Address).Distinct().ToHashSet();
            var missing = Enumerable.Range(0, 20).Where(a => !uniqueCoreAddresses.Contains(a)).ToList();
            if (missing.Count > 0)
                return new AssertionResult(false,
                    $"Missing Read replies for {missing.Count} core address(es): {string.Join(", ", missing)}.");

            return new AssertionResult(true, "All core register reads received after OpCtrl write.");
        }
        catch (Exception ex)
        {
            return new ErrorResult(ex);
        }
        finally
        {
            await RestoreOperationControlAsync(device, originalOpCtrl);
        }
    }

    private static async Task RestoreOperationControlAsync(VerifyConnection device, byte? value)
    {
        if (!value.HasValue)
            return;

        try
        {
            await device.CommandAsync(HarpMessage.FromByte(OperationControl.Address, MessageType.Write, value.GetValueOrDefault()));
        }
        catch
        {
        }
    }

    private static async Task<IResult> TestOptionalBitAsync(VerifyConnection device, string bitName, byte bitMask)
    {
        var original = await device.ReadByteAsync(OperationControl.Address);
        byte toggled = (byte)(original ^ bitMask);

        try
        {
            try
            {
                await device.CommandAsync(HarpMessage.FromByte(OperationControl.Address, MessageType.Write, toggled));
            }
            catch (HarpException)
            {
                return new Result<bool>(false, Status.Skipped,
                    $"{bitName} is optional/deprecated and not supported by this device.");
            }

            var readBack = await device.ReadByteAsync(OperationControl.Address);
            bool bitChanged = (readBack & bitMask) == (toggled & bitMask);

            return new AssertionResult(
                bitChanged,
                x => x
                    ? $"{bitName}: bit correctly toggled."
                    : $"{bitName}: bit did not change after write (expected {(toggled & bitMask) != 0}, got {(readBack & bitMask) != 0}).");
        }
        finally
        {
            await RestoreOperationControlAsync(device, original);
        }
    }
}
