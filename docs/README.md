# Harp Toolkit

Tool for inspecting, updating and interfacing with Harp devices, with automatic firmware and interface [code generation](https://harp-tech.org/toolkit/articles/generate.html).

## Getting Started

1. Navigate to the [Harp.Toolkit NuGet tool package](https://www.nuget.org/packages/Harp.Toolkit/)
2. Click `.NET CLI (Local)` and copy the two suggested commands. E.g.:

    ```cmd
    dotnet new tool-manifest # if you are setting up this repo
    dotnet tool install --local Harp.Toolkit
    ```

3. To view the tool help reference documentation, run:

    ```cmd
    dotnet harp.toolkit --help
    ```

4. To list all available system serial ports:

    ```cmd
    dotnet harp.toolkit list
    ```

5. To display info about a device connected to a specific serial port, with an optional timeout in milliseconds:

    ```cmd
    dotnet harp.toolkit --port COM4 --timeout 1000
    ```

6. To update the device firmware from a local HEX file:

    ```cmd
    dotnet harp.toolkit update --port COM4 --path Behavior-fw3.2-harp1.13-hw2.0-ass0.hex
    ```

7. To restore the tool at any point, run:

    ```cmd
    dotnet tool restore
    ```

## Code Generation

`harp.toolkit` can also generate device interface and firmware code from a `device.yml` metadata file. With a `device.yml` in the current directory, the following generates the .NET device interface, targeting [Bonsai.Harp](https://harp-tech.org/api/Bonsai.Harp.html):

```cmd
dotnet harp.toolkit generate interface
```

To generate the [Harp Python](https://harp-tech.org/python) interface instead:

```cmd
dotnet harp.toolkit generate interface python
```

See [Code Generation](https://harp-tech.org/toolkit/articles/generate.html) for authoring device metadata, generating firmware, and the available options.

## Contributing

Bug reports and contributions are welcome at [the GitHub repository](https://github.com/harp-tech/toolkit).

## License

`Harp.Toolkit` is released as open-source under the [MIT license](https://licenses.nuget.org/MIT).
