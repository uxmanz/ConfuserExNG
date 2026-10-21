# ConfuserExNG

**ConfuserExNG** is a modernized continuation of [Neo ConfuserEx](https://github.com/XenocodeRCE/neo-ConfuserEx), itself a fork of [ConfuserEx](https://yck1509.github.io/ConfuserEx/). It is an open-source .NET assembly obfuscator that uses [dnlib](https://github.com/0xd4d/dnlib/) for assembly manipulation.

ConfuserExNG supports eligible .NET Framework applications from **.NET Framework 2.0 through 4.8**, as well as modern .NET runtimes. Compatibility with **.NET 8 and .NET 9** has been verified for the fixes described below.

- **Releases:** https://github.com/uxmanz/ConfuserExNG/releases
- **Issues and bug reports:** https://github.com/uxmanz/ConfuserExNG/issues
- **Wiki:** https://github.com/uxmanz/ConfuserExNG/wiki

> **Important:** Obfuscation can change application behavior, especially when an application relies on reflection, serialization, dependency loading, WPF/BAML, or runtime-generated names. Test protected builds thoroughly before distributing them, and keep an unprotected build and backups.

## Contents

- [Highlights](#highlights)
- [Features](#features)
- [Requirements](#requirements)
- [Build from source](#build-from-source)
- [Command-line usage](#command-line-usage)
- [Quick Tool GUI](#quick-tool-gui)
- [Project files](#project-files)
- [Protection reference](#protection-reference)
- [Documentation and support](#documentation-and-support)
- [License and credits](#license-and-credits)

## Highlights

This fork includes improvements intended to make protection more predictable and easier to use:

- **Modern .NET compatibility:** fixes for runtime-loader issues involving `<Module>`-nested types, nested-type visibility, and forwarded-type resolution, allowing protected assemblies to load and run on .NET 8 and .NET 9.
- **Reflection-aware renaming:** compiler-generated string literals from `nameof(...)`, and string literals equal to a member's own name, prevent that member from being renamed. This helps preserve reflection-by-name behavior in libraries such as HarmonyLib.
- **Safer output handling:** project output follows `outputDir`; the CLI refuses to overwrite an input module when the resolved output path points to the same location.
- **Clearer CLI errors:** the CLI detects XML project files that are supplied without the expected `.crproj` extension and reports the issue.
- **Quick Tool GUI:** a small utility for protecting a single module without authoring a project file.

## Features

Depending on the protections you enable, ConfuserExNG provides:

- Symbol renaming, including WPF/BAML-aware and `nameof`-safe handling
- Anti-debugger and anti-profiler checks
- Anti-memory-dump protection
- Anti-tamper protection and method encryption
- Control-flow obfuscation
- Constant and resource encryption
- Reference-hiding proxies
- Experimental type scrambling
- Decompiler/IL-disassembler disruption
- Dependency embedding
- Output compression
- An extensible plugin API

Protection availability and behavior depend on the selected rules and parameters. These techniques increase the difficulty of inspecting an assembly; they do **not** guarantee that code cannot be reverse-engineered.

## Requirements

### Using the tools

Use a Windows environment compatible with the built binaries. The CLI project targets **.NET Framework 4.8**.

### Building from source

- Visual Studio 2017 or later, or a compatible MSBuild installation
- .NET Framework 4.8 build targeting support

The GUI can be built separately from the CLI.

## Build from source

Build the command-line tool in Release configuration:

```bat
msbuild Confuser.CLI\Confuser.CLI.csproj /p:Configuration=Release /p:Platform=AnyCPU
```

The CLI executable and component assemblies are placed in `Release\bin`.

To build the GUI separately, open or build:

```text
ConfuserEx\ConfuserEx.csproj
```

The Quick Tool source is in `Confuser.QuickTool`; it can also be launched from Visual Studio with **F5**.

## Command-line usage

The command-line executable is `Confuser.CLI.exe`, typically located in `Release\bin`. It supports two modes.

### 1. Protect a project file

```text
Confuser.CLI.exe [-n|--nopause] [-o|--out=<output directory>] <path to .crproj project file>
```

Example:

```bat
Confuser.CLI.exe -n MySolution.crproj
```

A `.crproj` file is an XML project that defines input modules, output paths, and protection rules. See [Project files](#project-files).

Path resolution:

- Paths in the project are resolved relative to its `baseDir`.
- A relative `baseDir` is resolved relative to the project file's location.
- `-o` overrides the project's `outputDir`.

### 2. Protect modules directly

Use this mode when you want to protect one or more assemblies without creating a full project file.

```text
Confuser.CLI.exe [-n|--nopause] -o <output directory> <modules...>
```

Example:

```bat
Confuser.CLI.exe -n -o protected MyApp.exe
```

Rules for direct-module mode:

- `-o` is **required**.
- The first module is treated as the main module; additional modules are protected together.
- A `.crproj` file may be supplied as the **last argument** as a rule template. Its rules, `seed`, `debug` setting, and plugin paths are reused for the generated project.

Example with a rule template:

```bat
Confuser.CLI.exe -n -o out MyApp.exe rules.crproj
```

If the first argument is a `.crproj` file, the CLI uses project-file mode. If the first file is XML with a `<project>` root but lacks the `.crproj` extension, the CLI stops and asks you to rename it rather than treating it as an assembly.

### CLI options

| Option | Description |
| --- | --- |
| `-n`, `--nopause` | Do not wait for a keypress after execution. Recommended for scripts, CI, and PowerShell pipelines. |
| `-o`, `--out=<directory>` | Output directory. Required in direct-module mode; overrides `outputDir` in project mode. |
| `--probe=<directory>` | Add a dependency search directory. May be repeated. |
| `--plugin=<path>` | Load a plugin from a directory or assembly. May be repeated. |
| `--debug` | Load `.pdb` files found beside input modules; equivalent to the project's `debug` attribute. |

### Exit codes

| Exit code | Meaning |
| ---: | --- |
| `0` | Obfuscation completed successfully. |
| `1` | Obfuscation failed; an error was logged. Suitable for failing a CI build. |

Without `-n`, interactive execution may pause for a keypress at the end. Use `-n` in automation to avoid hanging a script or pipeline.

### CLI examples

Protect a project and override its output directory:

```bat
Confuser.CLI.exe -n Release\MyApp.crproj -o Release\protected
```

Protect an executable from its own directory:

```bat
cd bin\Release
Confuser.CLI.exe -n -o ..\..\protected MyApp.exe
```

Protect an application and related plugin assemblies using a rule template:

```bat
Confuser.CLI.exe -n -o out MyApp.exe MyApp.PluginA.dll MyApp.PluginB.dll rules.crproj
```

Add dependency-probe directories and a plugin:

```bat
Confuser.CLI.exe -n -o out --probe=C:\ThirdParty\libs --plugin=myconfuserplugin.dll MyApp.exe
```

## Quick Tool GUI

`Confuser.QuickTool.exe` is a lightweight GUI for quick, single-module protection. It is useful for experiments and smoke tests when you do not need to author a `.crproj` file.

After building, find it in `Release\bin`.

### Interface overview

| Area | Description |
| --- | --- |
| **Input module** | Select an `.exe`, `.dll`, `.netmodule`, or `.winmd` file. If a neighboring PDB is found, **Load debug symbols** is selected automatically. |
| **Output** | Choose the destination directory. Leaving it at the source folder enables in-place testing. |
| **When replacing the original, save a backup** | Enabled by default; applies to in-place operation. |
| **Protections** | Select the protections to apply. The rule preset remains `none`; no additional protections are implicitly enabled by a preset. |
| **Advanced — seed** | Optional random seed. A fixed seed (for example, `1234`) makes repeated runs on the same input reproducible. |
| **Log** | Displays live engine messages. **Stop** cancels the run; **Open output** opens the result directory. |

### Available GUI protections

| Protection | Available setting |
| --- | --- |
| `rename` | Naming mode: `unicode` (default), `ascii`, `letters`, `decodable`, `sequential`, `reversible`, or `empty` |
| `ctrl flow` | Intensity from 0–100; default 60 |
| `constants` | Uses built-in defaults |
| `resources` | No additional parameter |
| `ref proxy` | Mild mode by default |
| `anti debug` | `win32` (default), `safe`, or `antinet` |
| `anti ildasm` | No additional parameter |
| `anti tamper` | `anti` (default), `jit`, or `normal` |
| `integrity` | No additional parameter |
| `anti dump` | No additional parameter |
| `typescramble` | Experimental |

Parameters become editable when the corresponding protection is selected.

### In-place test workflow

> **Caution:** In-place mode replaces the selected module after successful protection. Use a disposable build directory or ensure you have a valid backup.

1. Start `Confuser.QuickTool.exe`.
2. Select the module in your application's runtime directory, such as `...\win-x64\MyApp.dll`.
3. Leave the output folder set to the module's directory.
4. Select the protections you want to test. The documented defaults are `rename`, `ctrl flow`, `constants`, `ref proxy`, and `anti ildasm`.
5. Click **Obfuscate!** and monitor the log.
6. On success, the original is backed up (for example, `MyApp_orig.dll`; subsequent backups use names such as `MyApp_orig_2.dll`). If the module already has an `_orig` name, backups use `_backup` naming. The protected module replaces the original, and the temporary `.quickout` directory is removed.
7. Launch and test the application. To restore the original, restore the backup or rebuild/re-copy the original output.
8. If protection fails and the log shows `[ERROR]`, the original module remains untouched.

### Output to a separate directory

Choose a different output directory to write the protected module there. The source module remains unchanged. If debug symbols were loaded, the corresponding PDB is also written to the output directory.

### Headless mode

Quick Tool also supports a headless mode for scripts and CI. It uses the default protection set described above:

```bat
Confuser.QuickTool.exe headless <input .exe/.dll> <output directory | source folder to replace in place>
```

Exit code `0` indicates success; `1` indicates failure.

## Project files

A `.crproj` file is an XML document describing the modules to protect and the rules to apply. The full format is documented in [`docs/ProjectFormat.md`](docs/ProjectFormat.md) and validated by [`Confuser.Core/Project/ConfuserPrj.xsd`](Confuser.Core/Project/ConfuserPrj.xsd).

### Minimal structure

```xml
<project baseDir="bin\Release" outputDir="obfuscated" seed="42" debug="false">
  <module path="MyApp.exe" />
  <rule pattern="true" inherit="false" preset="normal" />
</project>
```

This is an illustrative starting point; adapt module paths and rules to your application.

### Project attributes

| Attribute | Description |
| --- | --- |
| `baseDir` | Base directory for relative paths. A relative value is resolved against the project file's location. Absolute paths are also supported. |
| `outputDir` | Directory for protected output. ConfuserExNG refuses to overwrite the input module if output resolves to the same location. |
| `seed` | Optional random-generator seed for reproducible runs. |
| `debug` | Optional Boolean. Set to `true` to load PDB files beside input modules for renaming analysis. Debug symbols are not usable together with packers. |

### Modules

```xml
<module path="MyApp.dll" />
```

- `path`: path to the module, relative to `baseDir` or absolute.
- `snKey`: optional path to a strong-name key used to sign the resulting module.
- `snKeyPass`: optional password for the strong-name key.
- `external="true"`: resolve the module as an external reference without obfuscating it.

Rules may also be placed inside a `<module>` element to apply them only to that module.

### Rules

Rules determine which protections apply to which targets and are evaluated in order.

```xml
<rule pattern="true" inherit="false" preset="normal">
  <protection id="rename" />
  <protection id="ctrl flow">
    <argument name="intensity" value="80" />
  </protection>
</rule>
```

Rule attributes and elements:

- `pattern`: expression evaluated against each target. Examples include `true`, `name('X')`, and `member-type('type') and full-name('NS.Type')`.
- `preset`: one of `none`, `minimum`, `normal`, `aggressive`, or `maximum`. The default is `none`.
- `inherit`: when `false`, the rule starts without inheriting previous applicable rule settings; the default is `true`.
- `<protection id="...">`: enables a protection. Use `action="remove"` to remove a protection inherited from a preset or earlier rule.
- `<argument name="..." value="..." />`: supplies a protection-specific parameter. `<parameter>` elements are also accepted.

For example, to exclude a public API type from renaming:

```xml
<rule pattern="member-type('type') and full-name('MyApp.PublicApi')" inherit="false">
  <protection id="rename" action="remove" />
</rule>
```

Working example projects for ILSpy and Paint.NET are included in the `additional` directory.

## Protection reference

| Protection ID | Purpose | Default preset level |
| --- | --- | --- |
| `rename` | Symbol renaming; WPF/BAML-aware and `nameof`-safe | `minimum` |
| `anti ildasm` | Disrupts IL disassemblers and decompilers | `minimum` |
| `anti debug` | Detects debuggers/profilers; modes include `safe`, `win32` (default), and `antinet` | `minimum` |
| `ctrl flow` | Control-flow obfuscation; `intensity` 0–100, default 60 | `normal` |
| `constants` | Constant encryption | `normal` |
| `resources` | Resource encryption | `normal` |
| `ref proxy` | Hides type/method references behind proxies; `mild` is the default mode | `normal` |
| `typescramble` | Generics-based type rearrangement; experimental | Manual only |
| `integrity` | Build-integrity verification | `maximum` |
| `anti dump` | Memory-dump protection | `maximum` |
| `anti tamper` | Tamper protection/method encryption; modes include `normal`, `jit`, and `anti` | `maximum` |
| `invalid metadata` | Emits unparsable metadata to disrupt decompilers | Manual only |

### Presets

A preset automatically enables protections at or below its level unless a protection is explicitly added or removed by the rule.

| Preset | Protections enabled |
| --- | --- |
| `none` | None |
| `minimum` | `rename`, `anti ildasm`, `anti debug` |
| `normal` | `minimum` protections plus `ctrl flow`, `constants`, `resources`, `ref proxy` |
| `aggressive` | Same protection set as `normal` |
| `maximum` | `normal` set plus `integrity`, `anti dump`, `anti tamper` |

`integrity`, `anti dump`, and `anti tamper` are reserved for the `maximum` level by default because they are among the most invasive protections. Enable them by selecting `preset="maximum"` or listing them explicitly.

## Documentation and support

- **Protection details and caveats:** [ConfuserExNG Wiki](https://github.com/uxmanz/ConfuserExNG/wiki)
- **Project XML format:** [`docs/ProjectFormat.md`](docs/ProjectFormat.md)
- **Project schema:** [`ConfuserPrj.xsd`](Confuser.Core/Project/ConfuserPrj.xsd)
- **Declarative obfuscation attributes:** [`docs/DeclarativeObfuscation.txt`](docs/DeclarativeObfuscation.txt)
- **Example projects:** `additional/`
- **Bug reports and feature requests:** [GitHub Issues](https://github.com/uxmanz/ConfuserExNG/issues)

When reporting an issue, include the ConfuserExNG version or commit, target runtime, a minimal reproducible project when possible, and the relevant log output. Do not include proprietary assemblies, secrets, signing keys, or passwords.

## License and credits

See [`LICENSE`](LICENSE) for license terms.

Credits:

- **[yck1509](https://github.com/yck1509)** — original author of [ConfuserEx](https://yck1509.github.io/ConfuserEx/).
- **[0xd4d](https://github.com/0xd4d)** — author of [dnlib](https://github.com/0xd4d/dnlib/), the .NET assembly manipulation library used by this project.
