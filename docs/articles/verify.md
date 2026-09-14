# Device Verification

`harp.toolkit` can check a device against the Harp specification and report where its behavior departs from what the standard requires. The checks span all three specification documents, covering the core register set and its access rules, the reply behavior required by the binary protocol, and alignment on the synchronization clock. Results print to the console as the run proceeds, and can be written to a shareable HTML report.

A verification result records how a device behaved against a stated version of the specification, and it confers no compliance status.

> [!Warning]
> Verification writes to device registers. Conformance cannot be established without exercising writes, read-only enforcement and event streams, so there is no read-only mode. Some checks leave the device clock and the operation control register in a changed state, and the run assumes a freshly powered device. An interrupted run can also leave the timestamp register locked until the device is power cycled. Avoid verifying a device that is part of a running experiment.

## Running a verification

A verification needs only the serial port of the device.

```ps1
dotnet harp.toolkit verify --port COM3
```

Every check reports as passed, failed, skipped or error. A failed check ran to completion and the device did not behave as required by the specification. An error means the check could not be completed at all, which happens when the device replies with an error. A check is skipped when it needs an option that was not supplied, and the message names the option.

Verification stops at the first request left unanswered for 2000 ms, since a late reply can be matched to a later check, and the console states where the run stopped. The HTML report is still written when `--report` is supplied, and states that the verification is incomplete.

#### Serial port
```ps1
--port <port>
```

Specifies the name of the serial port used to communicate with the device. This option is required.

#### Detailed results
```ps1
--verbose
```

Prints a detailed result for every check once the run finishes, including the statistics gathered by the measurements. Per-check progress is printed either way.

### Exit code

The command exits 1 if any check failed or ended in error, and 0 otherwise. Skipped checks do not affect the result, so a run that skips every optional check still exits 0. A run that cannot start or continue also exits 1, for example when the named serial port is not present or the device stops responding.

## Specification version

Harp devices do not all implement the same version of the standard, so no single set of checks applies to every device.

A device declares the version it implements in `R_VERSION`. Where that register is absent, unreadable or reads all zeros, the device is held to v1, since a device that predates the register also predates the version field. By default, checks belonging to a version outside that scope are neither run nor listed. A skipped result means a check that was in scope and did not run. The console and the report state how many checks were excluded.

#### Include prerelease checks
```ps1
--prerelease
```

Also runs the checks against the next specification revision, which is not yet ratified, regardless of the declared version. For a device that has not declared the next version, failures among them show what that version would require rather than defects against its own declared version. A failure here is worth checking carefully against the specification before it is treated as a device defect.

## Sharing a report

Console output is not an artifact. A report captures one run as a single HTML file that can be attached to an issue or a release.

```ps1
dotnet harp.toolkit verify --port COM3 --report report.html
```

The report is titled with the device name and opens with a header describing the run.

- **WhoAmI** is the device identity class, read from `R_WHO_AM_I`.
- **Serial port** is the port used to reach the device.
- **Hardware version** and **Firmware version** are read from the device at startup, and read as not reported for a device that does not answer them.
- **Protocol version declared** is what the device reports in `R_VERSION`, or that no version was declared.
- **Checked against** is the version of the specification used to verify the device, together with the reason when that is narrower than the version declared by the device.
- **Specification** links to the specification documents as they stood at the revision behind the checks.
- **Register set** names the generator package supplying the core register metadata, which fully determines the register set expected by the run.
- **Toolkit version** identifies the build of `harp.toolkit` that produced the report, including its source commit.

#### Report path
```ps1
--report <report>
```

Path of the HTML report written after the run. Without it the results are printed and not saved.

### Acting on a reported failure

The **Specification** link is what makes a disagreement decidable, so it is worth checking carefully before filing anything. Specification text moves between releases, and a check is written against one state of it.

If the device matches the text at that commit and a check still fails, the check is wrong, and that belongs in the toolkit repository. If a check matches the text and the text itself is wrong, that belongs in the protocol repository.

## Verifying the synchronization clock

Supplying a second device as a clock reference enables the alignment checks. Both devices must be connected to the same synchronization clock bus.

```ps1
dotnet harp.toolkit verify --port COM3 --clock-port COM4 --pps-event 32
```

#### Clock reference port
```ps1
--clock-port <clock-port>
```

Serial port of the reference clock device. Supplying it enables the clock alignment checks.

#### Tested device event register
```ps1
--pps-event <pps-event>
```

Address of the register on the tested device that reports the incoming pulse from the reference clock device. Supplying it enables the pulse alignment check, which also requires a clock reference port.

Note that the pulse is a physical output that only some devices produce, and it is distinct both from the synchronization signal on the clock bus and from the software heartbeat. A device reports the pulse through an application register of its own, which is why the address has to be supplied explicitly.

#### Sample count
```ps1
--clock-samples <clock-samples>
```

Number of pulse event pairs to collect for the alignment check. The default is 5, and the value must be greater than zero.

## Verifying the declared interface

A device can also be checked against its own declared interface rather than only against the standard. Supplying the device metadata generates an interface from it, reads every declared register from the live device, and parses each reply with the generated parsers. The identity, firmware and hardware versions declared in the metadata are cross-checked against the values reported by the device.

```ps1
dotnet harp.toolkit verify --port COM3 --metadata device.yml
```

#### Device metadata
```ps1
--metadata <metadata>
```

Path of the file describing the device registers. The file must exist.

Unlike code generation, this option has no default, so a `device.yml` located in the current directory does not automatically enable these checks.
