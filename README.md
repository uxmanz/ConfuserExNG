ConfuserExNG
--------

ConfuserExNG is a modernized continuation of the [Neo ConfuserEx](https://github.com/XenocodeRCE/neo-ConfuserEx)
fork of the [ConfuserEx](https://yck1509.github.io/ConfuserEx/) project — an open source C# obfuscator
that bundles its own copy of [dnlib](https://github.com/0xd4d/dnlib/) for assembly manipulation. It supports
all eligible .NET Frameworks (2.0 – 4.8) and modern .NET runtimes (including verified .NET 8 / .NET 9 support) and
provides decent, tunable obfuscation for your binaries.

If you have any questions or issues, please report them
[here](https://github.com/uxmanz/ConfuserExNG/issues). You can download the latest official release
[here](https://github.com/uxmanz/ConfuserExNG/releases).

## Modernization highlights

Recent contributions and fixes in this fork (aimed at making the tool reliable out of the box):

* **.NET 8 / .NET 9 compatible output.** Several runtime-loader compatibility issues were fixed (hidden
  `<Module>`-nested types, nested-type visibility, forwarder-type resolution), so protected assemblies both
  load *and run* on .NET 8 and .NET 9 (as well as .NET Framework 2.0 – 4.8).
* **Reflection-safe renaming.** String literals produced by the compiler for `nameof(...)` (and any literal equal to a
  member's own name) now veto the renaming of the matching member, so reflection-by-name lookups (e.g. `HarmonyLib`)
  keep working under full renaming.
* **Safe output handling.** Output always goes exactly where the project's `outputDir` says — protected modules are no
  longer silently written over the original inputs. If an output path would ever equal the input module's location,
  ConfuserEx refuses to overwrite it and reports an error instead.
* **Operator-friendly CLI fixes**, including a clear error when you accidentally feed the CLI an XML project file with
  the wrong extension.
* **ConfuserEx Quick Tool.** A small GUI utility (`Confuser.QuickTool.exe`) for one-click obfuscation of a single
  module — see [Quick Tool (GUI utility usage)](#quick-tool-gui-utility-usage) below.

Obfuscation Features
--------

* Supports .NET Framework 2.0/3.0/3.5/4.0/4.5 up to 4.8 and .NET (Core) 5+ runtimes
* Symbol renaming (supports WPF/BAML, `nameof`-safe)
* Protection against **debuggers/profilers**
* Protection against **memory dumping**
* Protection against **tampering (method encryption)**
* **Control flow** obfuscation
* **Constant/resources** encryption
* **Reference hiding** proxies
* **Type scrambler** obfuscation
* Disable **decompilers**
* **Embedding dependency**
* **Compressing output**
* Extensible plugin API

Building
--------

Confuser.CLI targets .NET Framework 4.8. Building from Visual Studio 2017 (or later) works out of the box, or from the
command line:

```bat
msbuild Confuser.CLI\Confuser.CLI.csproj /p:Configuration=Release /p:Platform=AnyCPU
```

All component assemblies plus the CLI executable are placed into the `Release\bin` directory. The GUI (`ConfuserEx`)
can be built separately from the `ConfuserEx\ConfuserEx.csproj` project.

CLI Usage
--------

The command line tool is `Confuser.CLI.exe` (located in `Release\bin` after building). It has **two invocation modes**.

### Mode 1: Protect using a project file

```
Confuser.CLI.exe [-n|--nopause] [-o|--out=<output directory>] <path to .crproj project file>
```

The project file (`.crproj`) is an XML document describing what to obfuscate and how — see
[Project file format](#project-file-format) below and `docs\ProjectFormat.md`.

```bat
Call Confuser.CLI.exe -n MySolution.crproj
```

* Paths inside the project are resolved relative to the project's own `baseDir` attribute.
* A relative `baseDir` is resolved relative to the location of the project file.
* The `-o` option, if given, overrides the project's `outputDir`.

### Mode 2: Protect one or more modules directly (no full project needed)

```
Confuser.CLI.exe [-n|--nopause] [-o|--out=<output directory>] <modules...>
```

```bat
Call Confuser.CLI.exe -n -o protected MyApp.exe
```

* **`-o` (`--out=`) is mandatory** in this mode: its value is taken as the output directory and the remaining
  arguments are treated as the modules to obfuscate.
* The first file is treated as the main module; subsequent files are additional modules to be obfuscated together.
* Optionally, you may pass a `.crproj` file **as the last argument**; in that case it is used as a *rule template*: its
  rules, `seed`, `debug` flag and plugin paths are carried over to the generated project, and the listed files are the
  modules to protect:

```bat
Call Confuser.CLI.exe -n -o out MyApp.exe rules.crproj
```

* If the first file ends with `.crproj` it is always treated as a project file (Mode 1).
* Safety net: if you pass a file that is actually a ConfuserEx project but lacks the `.crproj` extension, the CLI
  detects the XML `<project>` root, aborts, and tells you to rename it instead of obfuscating the XML.

### Options

| Option | Description |
| ------ | ----------- |
| `-n`, `--nopause` | Do not pause ("press any key") after the run finishes. Useful in scripts and CI: with this option the process exits immediately with a proper exit code. |
| `-o`, `--out=` | Output directory. Mandatory in module mode; overrides `outputDir` otherwise. |
| `--probe=` | Adds a probe directory: extra directories in which ConfuserEx searches for dependencies. Repeatable. |
| `--plugin=` | Adds a plugin path: a directory or assembly that contains custom ConfuserEx plugins. Repeatable. |
| `--debug` | Reads the debug symbols (*.pdb) that exist next to the input modules (same as the `debug` project attribute). |

### Exit codes

| Code | Meaning |
| ---- | ------- |
| `0` | Obfuscation finished successfully. |
| `1` | Obfuscation failed (an error was logged). Suitable for breaking a CI build. |

Note: without `-n`, and when running interactively (debugger attached, or no `PROMPT` environment variable — a
heuristic for interactive sessions), the CLI pauses and waits for a keypress before exiting. **Always pass `-n` in
scripts, continuous integration or PowerShell piping.**

### CLI examples

Protect a solution described by a project file, without interactive pause:

```bat
Confuser.CLI.exe -n Release\MyApp.crproj -o Release\protected
```

Obfuscate one executable directly (from its own directory — paths are relative to the current working directory):

```bat
cd bin\Release
Confuser.CLI.exe -n -o ..\..\protected MyApp.exe
```

Obfuscate an application with a set of plugin DLLs, reusing rules defined in a template project:

```bat
Confuser.CLI.exe -n -o out MyApp.exe MyApp.PluginA.dll MyApp.PluginB.dll rules.crproj
```

Add search directories and plugins:

```bat
Confuser.CLI.exe -n -o out --probe=C:\ThirdParty\libs --plugin=myconfuserplugin.dll MyApp.exe
```

Quick Tool (GUI utility usage)
----------

ConfuserExNG ships a small GUI utility for one-click obfuscation: `Confuser.QuickTool.exe`
(located in `Release\bin` after building; the source lives in the `Confuser.QuickTool` project and can also be
started from Visual Studio with F5). It is meant for **quick ad-hoc testing** — no `.crproj` authoring,
no copying files around.

### The window at a glance

| Area | What it does |
| ---- | ------------ |
| **Input module** | Type or browse to the `.exe` / `.dll` / `.netmodule` / `.winmd` you want to protect. If a PDB sits next to it, the *"Load debug symbols"* checkbox auto-ticks — leave it on for Smarter renaming analysis. |
| **Output** | The destination folder of the protected module. Leave it empty **or** select the source folder for in-place testing. |
| *"When replacing the original, save a backup"* | Enabled by default. Only relevant for in-place mode — see below. |
| **Protections** | One checkbox per protection; checked ones are applied exactly as configured (rule `preset` stays `none`, nothing else is auto-enabled). |
| **Advanced — seed** | Optional seed. Fill in a value (e.g. `1234`) to get reproducible, byte-identical protection runs of the same input. |
| **Log** | Live engine log — debug/info/warn/error lines appear as the protection pipeline runs. **Stop** cancels the run; **Open output** reveals the result in Explorer. |

### Protection options

| Protection checkbox | Parameter offered |
| ------------------- | ----------------- |
| `rename` | naming mode: `unicode` (default), `ascii`, `letters`, `decodable` (reversible-style, still obfuscated but decodable), `sequential`, `reversible`, `empty` |
| `ctrl flow` | `intensity` 0–100, default 60 |
| `constants` | *(none — sensible defaults)* |
| `resources` | *(none)* |
| `ref proxy` | *(none — mild mode by default)* |
| `anti debug` | mode: `win32` (default), `safe`, `antinet` |
| `anti ildasm` | *(none)* |
| `anti tamper` | mode: `anti` (default), `jit`, `normal` |
| `integrity` | *(none)* |
| `anti dump` | *(none)* |
| `typescramble` | *(none — experimental)* |

Newly enabled parameters become editable only when their protection is ticked.

### Typical test workflow (in-place)

1. Start `Confuser.QuickTool.exe`.
2. Select the module lying next to your application's runtime files (e.g. `...\win-x64\MyApp.dll`).
3. Leave the output folder as-is (it defaults to the module's own folder).
4. Tick the protections you're testing (defaults: rename + ctrl flow + constants + ref proxy + anti ildasm).
5. Press **Obfuscate!**. When the log reports success:
   * the original is first backed up as `MyApp_orig.dll` (next repeats produce `MyApp_orig_2.dll`, `MyApp_orig_3.dll`, ...;
     if the module is itself already named `*_orig`, backups become `*_backup`),
   * the protected module then **replaces the original in place** — just launch your application to test it,
   * the temporary `.quickout` working directory inside that folder is removed again.
6. To go back to the unprotected build, rename the `*_orig` file over the current one (or `dotnet build`/re-copy from your CI output).
7. When the obfuscation **fails** (log shows `[ERROR]`), the original stays untouched — nothing destructive happens.

### Distinct output folder

Point the output at any other folder and the protected module (plus a PDB when debug symbols were loaded) is
written **there** — the source file is never touched, mirroring what `Confuser.CLI -o` does.

### Headless (scripted) mode

The same code path can be driven without the window, using the default rules
(rename + ctrl flow + constants + ref proxy + anti ildasm) — handy for CI checks:

```bat
Confuser.QuickTool.exe headless <input .exe/.dll> <output dir | source folder to replace in place>
```

Exit code `0` = success, `1` = failure.

Project file format
--------

A `.crproj` file is an XML document. The full grammar is documented in `docs\ProjectFormat.md` and validated by the
schema `Confuser.Core\Project\ConfuserPrj.xsd`. Below is a brief but complete walkthrough.

### Root element

```xml
<project baseDir="bin\Release" outputDir="obfuscated" [seed="42"] [debug="false"]>
  ...
</project>
```

| Attribute | Description |
| --------- | ----------- |
| `baseDir` | Base directory for all relative paths used in the project. A relative value is resolved against the project file's location. Absolute paths (including absolute `module` paths and drive letters) are supported. |
| `outputDir` | Directory where obfuscated files are placed. ConfuserEx will never overwrite the original input module when doing so (see Modernization highlights). |
| `seed` | Seed of the random generator — makes repeated obfuscation runs reproducible. Optional. |
| `debug` | `true` to load the debug symbols (*.pdb) located next to the input modules (useful for renaming analysis; note that debug symbols are not usable together with packers). Optional. |

### Modules

```
<module path="MyApp.dll" [snKey="key.snk" snKeyPass="..."] />
```

* `path` — path of the module, relative to `baseDir` (absolute paths are also supported).
* `snKey` / `snKeyPass` — optional path to a strong name key used to sign the resulting module.
* `external="true"` — the module will *not* be obfuscated, only used for reference resolution.

A rule can also be declared *inside* a module: it then only applies to that module.

### Protection rules

Rules decide **which members** get **which protections**, in order:

```xml
<rule pattern="true" inherit="false" preset="normal">
  <protection id="rename" />
  <protection id="ctrl flow">
    <argument name="intensity" value="80" />
  </protection>
</rule>
```

* `pattern` — a pattern expression evaluated for every target. Examples:
  * `true` — matches everything.
  * `name('X')` — matches every member named `X`.
  * `member-type('type') and full-name('NS.Type')` — matches types with that full name.
* `preset` — one of `none`, `minimum`, `normal`, `aggressive`, `maximum` (details below). Default `none`.
* `inherit` — if set to `false`, the rule does not inherit the settings of previous rules and *starts from nothing*;
  otherwise it builds on top of all earlier applicable rules. Default `true`.
* `protection` elements:
  * `id` — the protection identifier (see table below).
  * `action="add"` (default) or `action="remove"` — remove is handy to carve exceptions *after* a preset was applied,
    e.g. exclude a license-check type from renaming.
  * child elements `<argument name="..." value="..." />` pass protection-specific parameters
    (`<parameter>` elements are also accepted).

Example of describing exclusions: keep certain members' names and keep public visibility untouched:

```xml
<rule pattern="member-type('type') and full-name('MyApp.PublicApi')" inherit="false">
  <protection id="rename" action="remove" />
</rule>
```

Working example projects (for ILSpy and Paint.NET) are included in the `additional` directory.

### Protections

| `id` in rules | Protection | Default preset level |
| ------------- | ---------- | -------------------- |
| `rename` | Symbol renaming (WPF/BAML aware, `nameof`-safe) | minimum |
| `anti ildasm` | Prevents decompilers/IL disassemblers | minimum |
| `anti debug` | Anti-debugger/profiler detection (modes: `safe`, `win32` (default), `antinet`) | minimum |
| `ctrl flow` | Control flow obfuscation (parameter `intensity`, 0–100, default `60`) | normal |
| `constants` | Constants encryption | normal |
| `resources` | Resources encryption | normal |
| `ref proxy` | Hides type/method references behind proxies (mode: `mild` default, higher modes slower) | normal |
| `typescramble` | Type scrambler (generics-based type rearranging) — experimental | none (manual) |
| `integrity` | Build verification | maximum |
| `anti dump` | Memory-dump protection | maximum |
| `anti tamper` | Tamper protection / method encryption (modes: `normal`, `jit`, `anti`) | maximum |
| `invalid metadata` | Emits unparsable metadata to break decompilers | none (manual) |

### Presets

A rule's `preset` auto-enables every protection whose default level is at or below the given level (unless the
protection is listed explicitly in the rule):

| Preset | Auto-enabled protections |
| ------ | ------------------------ |
| `none` | *(nothing)* |
| `minimum` | rename, anti ildasm, anti debug |
| `normal` | minimum + ctrl flow, constants, resources, ref proxy |
| `aggressive` | same set as `normal` |
| `maximum` | previous + integrity, anti dump, anti tamper |

Note: `integrity`, `anti dump` and `anti tamper` live at the `maximum` level *only*; to enable them your rule must set
`preset="maximum"` or list them explicitly (this is intentional: they are the most invasive protections).

Documentation
--------

Each individual protection is explained on the
[WIKI](https://github.com/uxmanz/ConfuserExNG/wiki) page (which protection it is, when to use it, its
parameters and possible pitfalls). Documentation for the project XML format can be found in
[docs\ProjectFormat.md](docs/ProjectFormat.md) and the `.xsd` schema in
[Confuser.Core\Project\ConfuserPrj.xsd](Confuser.Core/Project/ConfuserPrj.xsd).

Declarative (in-code) obfuscation attributes are described in [docs\DeclarativeObfuscation.txt](docs/DeclarativeObfuscation.txt).

Bug Report
----------

See the [Issue tracker](https://github.com/uxmanz/ConfuserExNG/issues) of this repository.

License
-------

See [LICENSE](LICENSE) for details.

Credits
-------

* **[yck1509](https://github.com/yck1509)** — the one and only, original author of
  [ConfuserEx](https://yck1509.github.io/ConfuserEx/)
* **[0xd4d](https://github.com/0xd4d)** — for his awesome work and extensive knowledge
* Members of the **[Black Storm forum](http://board.b-at-s.info/)** for their help!
