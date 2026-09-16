# Firmware Update

`harp.toolkit` can write a firmware image to a connected device over its serial port, using the bootloader built into the ATxmega core. The image states the device name and hardware version it targets, and the update checks it is compatible before writing anything.

Devices built on the Pico core are not currently supported. They update through a different process, and their firmware is distributed as `.uf2` images rather than Intel HEX.

> [!Warning]
> An update resets the device, so avoid updating firmware that is part of a running experiment. Interrupting an update leaves the device in bootloader mode, where it stops answering Harp commands until an update completes successfully. This is a fail-safe rather than damage, and [Recovering a device in bootloader mode](#recovering-a-device-in-bootloader-mode) describes the recovery.

## Updating a device

An update needs the serial port and a firmware image.

```text
dotnet harp.toolkit update --port COM3 Behavior-fw3.3-harp1.15-hw2.0-ass0.hex
```

The command reads the device name and hardware version, checks that the image is compatible, resets the device into its bootloader, writes the image one page at a time, and then leaves the bootloader so the new firmware starts. Progress reports the current stage alongside a percentage for the whole update. Only a retry of the bootloader decreases the percentage.

A device takes a moment to answer Harp again once an update finishes, measured at a median of about two seconds and occasionally over ten. The command waits for the device to answer, for up to twenty seconds, so a successful run ends with the device ready. Any other tool that connects to the device straight after an update may find the device unresponsive and should wait in the same way before treating silence as a failure.

#### Firmware image
```text
<firmware>
```

Path to the firmware image in Intel HEX format. The file must exist, and its name must follow the convention described in [Firmware image names](#firmware-image-names). This argument is required.

#### Serial port
```text
--port <port>
```

Name of the serial port used to communicate with the device. This option is required.

#### Response timeout
```text
--timeout <timeout>
```

Time in milliseconds to wait for the device to answer each Harp command. It applies while the update reads the device identity and requests the reset. The default is 2000, and `-1` waits indefinitely. Increase it if the device is slow to answer, for example immediately after an earlier update. It does not affect the bootloader protocol or the wait for the device after an update, which have their own timeouts.

### Canceling an update

Ctrl+C cancels an update while it reads the device identity and checks the image. The device is left as it was, and the command reports that the update was canceled before the device was reset.

Once the update resets the device, it ignores the cancellation and runs to completion. The terminal still ends the process after a short grace period, so an update interrupted while writing leaves the device in bootloader mode.

### Exit code

The command exits 0 once the image is written and the device answers Harp again, and 1 otherwise. Every failure is reported as a message rather than a stack trace. A failure after the device has been reset also reports the percentage reached.

The command also exits 1 when the device stays silent after the image is written.

## Firmware image names

The update reads the device and version information from the file name, not from the contents of the image. The name must follow this convention:

```text
<device>-fw<firmware>-harp<core>-hw<hardware>-ass<assembly>.hex
```

`<device>` is the device name reported by `R_DEVICE_NAME`. `<firmware>`, `<core>` and `<hardware>` are two-part versions, and `<assembly>` is the board assembly number. A preview build adds a `-preview<number>` suffix.

An image built for a range of hardware revisions states `x` in place of a hardware version component. For example, `hw1.x` marks an image that is valid for every revision of hardware 1.

The update refuses an image when the device name does not match the connected device, or when the device does not satisfy the hardware version. The message names both sides, so the mismatch is visible without opening the file.

## Recovering a device in bootloader mode

The bootloader records that a page was written, and stays in control until an update completes. That record survives a power cycle. A disconnected cable or a closed terminal can therefore leave the device in bootloader mode, without going back to the firmware it had before.

A device in that state does not answer Harp commands, so an ordinary update cannot read its identity and cannot reach it. The update detects the condition and reports it:

```text
The device on the serial port COM3 specified with --port did not respond in time. The device
is in bootloader mode, left there by an interrupted update. Re-run with --force, which skips
the compatibility check when the device cannot answer.
```

Repeat the update with `--force` to recover the device.

```text
dotnet harp.toolkit update --port COM3 --force Behavior-fw3.3-harp1.15-hw2.0-ass0.hex
```

If the forced update does not reach the device either, power cycle it first and repeat. A partial page in the receive buffer holds the bootloader until it is reset.

#### Force an update
```text
--force
```

Writes the image without the compatibility check.

The flag covers two situations that cannot be separated. A device in bootloader mode reports no identity, so there is nothing to check the image against. Recovering a device in that state therefore requires this flag.

The other use is firmware development. Do not use the flag only because an update was refused. The flag removes the only check that prevents an update with an image targeting a different device. If an image names a different device, it is usually the wrong image.
