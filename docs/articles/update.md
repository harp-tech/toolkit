# Firmware update

`harp.toolkit` can write a firmware image to a connected device. A device built on the ATxmega core is updated over its serial port, through the bootloader built into the core, as described in [Devices built on the ATxmega core](#devices-built-on-the-atxmega-core). A device built on the Pico core is updated over USB through the boot ROM of the RP2040 or RP2350, as described in [Devices built on the Pico core](#devices-built-on-the-pico-core). The image states the device name and hardware version it targets, and the update checks it is compatible before writing anything.

> [!Warning]
> An update resets the device, so avoid updating firmware that is part of a running experiment. Interrupting an update leaves the device in bootloader mode, or in BOOTSEL mode on the Pico core, where it stops answering Harp commands until an update completes successfully. The device remains recoverable, as described in [Recovering a device in bootloader mode](#recovering-a-device-in-bootloader-mode) or [Recovering a device in BOOTSEL mode](#recovering-a-device-in-bootsel-mode).

## Updating a device

An update needs the serial port and a firmware image.

```text
dotnet harp.toolkit update --port COM3 Behavior-fw3.3-harp1.15-hw2.0-ass0.hex
```

The command reads the device name and hardware version, checks that the image is compatible, resets the device into its bootloader, writes the image, and then restarts the device so the new firmware starts. Progress reports the current stage alongside a percentage for the whole update. Only a retry of the bootloader decreases the percentage.

A device takes a moment to answer Harp again once an update finishes, measured at a median of about two seconds and occasionally over ten. The command waits for the device to answer, for up to twenty seconds, so a successful run ends with the device ready. Any other tool that connects to the device straight after an update may find the device unresponsive and should wait in the same way before treating silence as a failure.

#### Firmware image
```text
<firmware>
```

Path to the firmware image, in Intel HEX format for a device built on the ATxmega core, or in UF2 format for a device built on the Pico core. The file must exist, and its name must follow the convention described in [Firmware image names](#firmware-image-names). This argument is required.

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

#### Force an update
```text
--force
```

Writes the image without the compatibility check.

One use is recovering a device in bootloader mode, or in BOOTSEL mode on the Pico core. A device in that state reports no identity, so there is nothing to check the image against.

The other use is firmware development. Do not use the flag only because an update was refused. The flag removes the only check that prevents an update with an image that targets a different device. If the name of an image refers to a different device, it is usually the wrong image.

### Canceling an update

Ctrl+C cancels an update while it reads the device identity and checks the image. The device is left as it was, and the command reports that the update was canceled before the device was reset.

Once the update resets the device, it ignores the cancellation and runs to completion. The terminal still ends the process after a short grace period, so an update interrupted while writing leaves the device in bootloader mode, or in BOOTSEL mode on the Pico core.

### Exit code

The command exits 0 once the image is written and the device answers Harp again, and 1 otherwise. Every failure is reported as a message rather than a stack trace. A failure after the device has been reset also reports the percentage reached.

The command also exits 1 when the device stays silent after the image is written.

## Firmware image names

The update reads the device and version information from the file name, not from the contents of the image. The name must follow this convention:

```text
<device>-fw<firmware>-harp<core>-hw<hardware>-ass<assembly>.hex
```

`<device>` is the device name reported by `R_DEVICE_NAME`. `<firmware>`, `<core>` and `<hardware>` are two-part versions, and `<assembly>` is the board assembly number. A preview build adds a `-preview<number>` suffix.

An image for a device built on the Pico core follows the same convention with the `.uf2` extension.

An image built for a range of hardware revisions states `x` in place of a hardware version component. For example, `hw1.x` marks an image that is valid for every revision of hardware 1.

The update refuses an image when the device name does not match the connected device, or when the device does not satisfy the hardware version.

> [!Note]
> A device with a coprocessor, such as the SoundCard, publishes a second image in each release, with a `.PIC32` suffix after the device name, or `.X` in older releases. The update writes both images, and requires the coprocessor image to sit next to the firmware image under the same name with the suffix.

## Devices built on the ATxmega core

On the ATxmega core, the bootloader is part of the core and is accessible over the same serial port. The update resets the device into the bootloader through `R_RESET_DEV` and writes the image one page at a time.

### Recovering a device in bootloader mode

The bootloader records that a page was written, and stays in control until an update completes. The record is kept through a power cycle, so a disconnected cable or a closed terminal can leave the device in bootloader mode, without going back to the firmware it had before.

A device in that state does not answer Harp commands, so an ordinary update cannot read its identity or communicate with it. The update detects the condition and reports it:

```text
The device on the serial port COM3 specified with --port did not respond in time. The device
is in bootloader mode, left there by an interrupted update. Re-run with --force, which skips
the compatibility check when the device cannot answer.
```

Repeat the update with `--force` to recover the device.

```text
dotnet harp.toolkit update --port COM3 --force Behavior-fw3.3-harp1.15-hw2.0-ass0.hex
```

If the forced update cannot communicate with the device either, power cycle it first and repeat. A partial page in the receive buffer holds the bootloader until it is reset.

## Devices built on the Pico core

On the Pico core, the bootloader is the boot ROM of the RP2040 or RP2350. The update sets the `UPDATE_FIRMWARE` bit of `R_RESET_DEV`, and the device restarts in BOOTSEL mode, where its serial port disappears and the boot ROM exposes a USB interface instead. Besides the compatibility check made over Harp, the update checks in this mode that the image targets the microcontroller, and reads it back after writing.

If the image targets a different microcontroller, the update restarts the device with its previous firmware. Any other failure after the restart leaves the device in BOOTSEL mode, as described in [Recovering a device in BOOTSEL mode](#recovering-a-device-in-bootsel-mode).

### Platform setup

The update communicates with the boot ROM through the libusb 1.0 library.

- On Windows, the tool includes libusb, which needs the [latest Microsoft Visual C++ Redistributable](https://learn.microsoft.com/cpp/windows/latest-supported-vc-redist). The boot interface of the RP2040 also needs a WinUSB driver, which can be bound with a tool such as Zadig. The RP2350 needs no driver.
- On Linux, install the libusb package of the distribution. Opening the boot interface without root access needs a udev rule, such as the rule provided by picotool.
- On macOS, the tool includes libusb. No other setup is needed.

### Recovering a device in BOOTSEL mode

A device in BOOTSEL mode does not answer Harp commands, so an ordinary update cannot read its identity. The update reports when a device in BOOTSEL mode is attached, and a forced update writes the image to it.

```text
dotnet harp.toolkit update --port COM5 --force Hobgoblin-fw0.2-harp2.0-hw1.0-ass0.uf2
```

A forced update writes to a device in BOOTSEL mode only when it is the only one attached, so disconnect any other device in BOOTSEL mode first.
