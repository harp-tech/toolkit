## About

`Harp.Templates` provides templates for creating new [Harp](https://harp-tech.org/) device repositories, including automatic generation of firmware and [Bonsai](https://bonsai-rx.org/) interface code from a device shema.

## How to Use

### Installation

```
dotnet new install Harp.Templates::0.4.0
```

### Creating a Device Project

```
dotnet new harpdevice -n <project-name>
```

### Run code generation

```
dotnet build Generators
```

## Additional Documentation

For additional documentation and examples, refer to [the GitHub repository](https://github.com/harp-tech/toolkit).

## Feedback & Contributing

`Harp.Templates` is released as open-source under the [MIT license](https://licenses.nuget.org/MIT). Bug reports and contributions are welcome at [the GitHub repository](https://github.com/harp-tech/toolkit).