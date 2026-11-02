# Code Generation

`harp.toolkit` generates firmware code, interface code and reference documentation from device metadata.

## Editing device metadata

1. Install [Visual Studio Code](https://code.visualstudio.com/).
2. Install the [YAML extension](https://marketplace.visualstudio.com/items?itemName=redhat.vscode-yaml).

The device interface is described in a `device.yml` file. The file must specify every application register of the device, together with the bit masks, group masks and payload formats that the registers use.

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

A device interface can be generated from the `device.yml` metadata file, in either of two target languages. In both, the generated code declares a type for every register, together with the enum and payload types that the registers use, and a map from register address to type.

A register or payload member may also declare a `converter`, for an `interfaceType` the generator cannot synthesize from the metadata alone. The implementation is then written by hand and referenced by the generated code.

The target language must be specified, followed by an optional metadata path. Without a path the generator reads `device.yml` from the current directory.

```text
dotnet harp.toolkit generate interface csharp path/to/device.yml
```

### .NET interface

The interface targets [Bonsai.Harp](https://harp-tech.org/api/Bonsai.Harp.html) for reactive programming.

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

The interface targets [Harp Python](https://harp-tech.org/python).

```text
dotnet harp.toolkit generate interface python
```

Custom converters are supplied in a companion `converters` module, which the generated module imports.

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

The target core must be specified, followed by an optional metadata path. Without a path the generator reads `device.yml` from the current directory.

```text
dotnet harp.toolkit generate firmware atxmega path/to/device.yml
```

The following options are available.

#### IO pin configuration
```text
--ios <ios>
```

Specifies the path to the file that describes the device IO pins. Each entry describes a pin, with its port, direction and initial state. The generator emits an `init_ios` function that configures the pins, together with read and write macros named after each pin. This file is required, and the default path is `ios.yml` in the current directory.

#### Generate implementation stubs
```text
--implementation
```

Specifies whether to generate implementation stubs. In general, use this option only to start development of a new ATxmega device.

> [!Warning]
> This replaces the implementation files, including any code already written into them. Generate into an empty directory to compare against an existing implementation.

## Generating device documentation

Device reference documentation is generated from the same `device.yml` file as the device interface, with one page for each public register. Each page shows the details of the register, such as address, payload type, access, default value and range, followed by the members of its payload and the values of every mask that the register uses. All content is generated for a specific site generator. Currently, [docfx](https://dotnet.github.io/docfx/) is the only supported site generator.

The target site generator must be specified, followed by an optional metadata path. Without a path the generator reads `device.yml` from the current directory. The output location is always required.

```text
dotnet harp.toolkit generate documentation docfx path/to/device.yml -o artifacts/docs/registers
```

### Docfx reference pages

The generator writes the following files:

- A docfx UniversalReference page for each public register.
- A page for each payload type and mask, kept as a cross-reference target and not listed in the table of contents.
- A `toc.yml` file that lists the register pages.
- A `registers.md` file with a table of all registers, written as a fragment to include in another page.

Each register page has the UID of the corresponding register type declared by the .NET interface, together with the UIDs of its timestamped type and of the types that create its payloads. Cross-references to any of these types therefore lead to the register page, and the Bonsai editor opens the register page as the help for any of these operators.

The pages render with the template in [harp-tech/docfx-tools](https://github.com/harp-tech/docfx-tools), which the site must include.

> [!Note]
> A site that also builds the .NET API reference must exclude from it every .NET type documented by a generated page, so that each UID is declared once.

> [!Tip]
> A hand-written overwrite page in `apidoc/` for the register UID still applies, so usage notes and workflow examples can be added to the generated page.

The following options are available to configure the generated output.

#### Namespace
```text
-ns, --namespace <ns>
```

Specifies the namespace of the page UIDs. It must match the namespace of the generated .NET interface. The default is `Harp.DeviceName`, the same as for the .NET interface.

## Generating device metadata

The device metadata and IO pin configuration files can be generated from legacy XLS worksheet files that describe the device. Both the `device.yml` and `ios.yml` will be generated from a single `registers.xls` file.

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

Specifies the location where to place the generated output. The folder is created if it does not exist. The default is the current directory, except for documentation, where the output location is required. For an interface this is usually the folder of the `.csproj` project, and for firmware the folder of the `.cproj` project.

## Exit code

Every generation command exits 0 once the output is written, and 1 otherwise. A command exits 1 when the metadata file cannot be loaded or fails validation, or when code generation reports errors. The reason is reported as a message. Warnings alone do not affect the result, so a command that reports only warnings still writes its output and exits 0.
