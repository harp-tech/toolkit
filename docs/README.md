# Harp Toolkit

Tool for inspecting, updating and interfacing with Harp devices, with automatic firmware and interface [code generation](https://harp-tech.org/toolkit/articles/generate.html).

## Getting Started

1. Navigate to the [Harp.Toolkit NuGet tool package](https://www.nuget.org/packages/Harp.Toolkit/)
2. Click `.NET CLI (Local)` and copy the two suggested commands. E.g.:

    ```text
    dotnet new tool-manifest
    dotnet tool install --local Harp.Toolkit
    ```

    The first command only needs to be run when setting up a new repository.

3. To view the tool help reference documentation, run:

    ```text
    dotnet harp.toolkit --help
    ```

4. To list all available system serial ports:

    ```text
    dotnet harp.toolkit list
    ```

5. To display info about a device connected to a specific serial port:

    ```text
    dotnet harp.toolkit --port COM4
    ```

    Each read waits up to 2000 milliseconds for a response. Pass `--timeout` to change that default, or `--timeout -1` to wait indefinitely.

6. To restore the tool at any point, run:

    ```text
    dotnet tool restore
    ```

## Firmware Update

`harp.toolkit` can write a firmware image to a connected device, checking that the image is compatible before writing anything:

```text
dotnet harp.toolkit update --port COM4 Behavior-fw3.3-harp1.15-hw2.0-ass0.hex
```

An update resets the device, so avoid updating firmware that is part of a running experiment, since interrupting an update leaves the device in bootloader mode until one completes successfully.

See [Firmware Update](https://harp-tech.org/toolkit/articles/update.html) for the naming convention used by firmware images, how to recover a device left in bootloader mode, and the available options.

## Code Generation

`harp.toolkit` can also generate device interface and firmware code from a `device.yml` metadata file. With a `device.yml` in the current directory, the following generates the .NET device interface, targeting [Bonsai.Harp](https://harp-tech.org/api/Bonsai.Harp.html):

```text
dotnet harp.toolkit generate interface
```

To generate the [Harp Python](https://harp-tech.org/python) interface instead:

```text
dotnet harp.toolkit generate interface python
```

See [Code Generation](https://harp-tech.org/toolkit/articles/generate.html) for authoring device metadata, generating firmware, and the available options.

## Device Verification

`harp.toolkit` can also check a device against the Harp specification, reporting where its behavior departs from the standard and writing the result as a shareable HTML report:

```text
dotnet harp.toolkit verify --port COM4 --report report.html
```

Verification writes to device registers and assumes a freshly powered device, so avoid running it against a device that is part of a running experiment.

See [Device Verification](https://harp-tech.org/toolkit/articles/verify.html) for the specification used to check the device, the report structure, and the available options.

## Contributing

Bug reports and contributions are welcome at [the GitHub repository](https://github.com/harp-tech/toolkit).

## License

`Harp.Toolkit` is released as open-source under the [MIT license](https://licenses.nuget.org/MIT).
