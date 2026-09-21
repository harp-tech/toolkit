# Code Generation

`harp.toolkit` can be used to automatically generate firmware and interface code from device metadata.

## Editing device metadata

1. Install [Visual Studio Code](https://code.visualstudio.com/)
2. Install the [YAML extension](https://marketplace.visualstudio.com/items?itemName=redhat.vscode-yaml).

The device interface can be described using a `device.yml` file. A complete specification of all device registers, including bit masks, group masks, and payload formats needs to be provided.

```yaml
%YAML 1.1
---
# yaml-language-server: $schema=https://harp-tech.org/draft-02/schema/device.json
device: DeviceName
whoAmI: 0000
firmwareVersion: "0.1"
hardwareTargets: "0.0"
registers:
  DigitalInputs:
    address: 32
    type: U8
    access: Event
```

## Generating device interface code

A device interface can be generated from the `device.yml` metadata file, in either of two target languages. In both, the generated code declares a type for every register, together with the enum and payload types they are built from, and a map from register address to type.

A register or payload member may also declare a `converter`, for an `interfaceType` the generator cannot synthesize from the metadata alone. The implementation is then written by hand and referenced by the generated code.

The target language is always named, and an optional metadata path follows it. Without a path the generator reads `device.yml` from the current directory.

```text
dotnet harp.toolkit generate interface csharp path/to/device.yml
```

### .NET interface

An interface for reactive programming targeting [Bonsai.Harp](https://harp-tech.org/api/Bonsai.Harp.html).

```text
dotnet harp.toolkit generate interface csharp
```

Registers are additionally exposed as [operators](https://harp-tech.org/articles/operators.html), alongside an asynchronous API for use from .NET applications.

Custom converters are supplied by implementing the generated `ParsePayload` and `FormatPayload` partial methods in a hand-written partial class.

The following options are available to configure the generated output.

#### Namespace
```text
-ns, --namespace <ns>
```

Specifies the namespace for the generated code. The default namespace is `Harp.DeviceName` where `DeviceName` is the name of the device specified in the `device.yml` file.

### Python interface

An interface targeting [Harp Python](https://harp-tech.org/python).

```text
dotnet harp.toolkit generate interface python
```

Custom converters are supplied in a companion `converters` module, which the generated module imports from.

The `--namespace` option is not available for this target, since the generated module declares no namespace.

The following options are available to configure the generated output.

#### Package
```text
--package
```

Specifies whether to generate the interface as a package. The interface is written as `__init__.py`, together with a `py.typed` marker that declares the package as typed, so the output directory alone determines the import path. An output directory of `src/mypackage/mydevice` produces the `mypackage.mydevice` package, so a device repository needs no re-export module. Without this option the interface is written as a single `device.py` module, and the same output directory produces `mypackage.mydevice.device`.

## Generating device firmware code

Device firmware code is generated for a specific Harp core, since firmware typically targets particular microcontroller hardware together with its SDK and build toolchain, none of which is universal. The [Harp Core ATxmega](https://harp-tech.org/core.atxmega/) is currently the only supported core.

The generator writes two kinds of file. The headers are always written, and everything in them follows from `device.yml`, down to the name of every register and mask, so they can be regenerated whenever the metadata changes. The implementation files are written only on request, and give every register a function to fill in. Since the device metadata does not describe the body of each function, the implementation is then written by hand.

The target core is always named, and an optional metadata path follows it. Without a path the generator reads `device.yml` from the current directory.

```text
dotnet harp.toolkit generate firmware atxmega path/to/device.yml
```

The following options are available.

#### IO pin configuration
```text
--ios <ios>
```

Specifies the path to the file that describes the device IO pins. Each entry names a pin with its port, direction and initial state. The generator emits an `init_ios` function that configures the pins, together with read and write macros named after each pin. This file is required, and the default path is `ios.yml` in the current directory.

#### Generate implementation stubs
```text
--implementation
```

Specifies whether to generate implementation stubs. In general this should be run only when starting development of a new ATxmega device.

> [!Warning]
> This replaces the implementation files, including any code already written into them. Generate into an empty directory to compare against an existing implementation.

## Generating device metadata

The device metadata and IO pin configuration files can be generated from legacy XLS worksheet files describing the device. Both the `device.yml` and `ios.yml` will be generated from a single `registers.xls` file.

```text
dotnet harp.toolkit generate metadata <registers.xls>
```

> [!Warning]
> The `registers.xls` file format is deprecated and is no longer recommended for editing device metadata as it lacks important features of the new YAML format, including complex payload spec formats and mixed access registers.

## Output location

Every generation command accepts an output location.

```text
-o, --output <o>
```

Specifies the location where to place the generated output. The default is the current directory. For an interface this usually points to the folder of your `.csproj` project, and for firmware to the folder of your `.cproj` project.
