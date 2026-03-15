# Harp Toolkit

Tool for inspecting, updating and interfacing with Harp devices.

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

5. To display info about a device connected to a specific serial port:

    ```cmd
    dotnet harp.toolkit --port COM4
    ```

6. To update the device firmware from a local HEX file:

    ```cmd
    dotnet harp.toolkit update --port COM4 --path Behavior-fw3.2-harp1.13-hw2.0-ass0.hex
    ```

7. To restore the tool at any point, run:

    ```cmd
    dotnet tool restore
    ```
