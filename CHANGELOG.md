# Changelog

## 1.4.0 — Anti-detection hardening, deobfuscator resistance, generated decoders (2026-10-24)

### Changed
- Anti-detection hardening: obfuscated assemblies no longer carry the static fingerprints that antivirus vendors match on. The embedded watermark attribute (previously `ConfusedByAttribute` with the tool version) now uses randomized names and content; the well-known `Confuser.Runtime.*` namespace and type names of the embedded runtime components (anti-debug, anti-dump, anti-tamper, constants, ...) are randomized per module right before writing; the hidden nesting-container type no longer carries a tool-identifying name; and unicode rename-mode names mix characters from several Unicode planes with a per-run invisible terminator instead of the fixed same-length, single-range, U+202E-terminated pattern that public detection rules match on. Behavior of the protections is unchanged.
- Deobfuscator resistance: the runtime scaffolding for constants decryption, LZMA decompression and resource decryption is now injected into a randomly named ordinary type instead of the module global type. Static deobfuscators (de4dot-cex and derivatives) locate the protection by scanning global-type methods for the known decryptor/LZMA shapes; with the helpers relocated they no longer identify the assembly as ConfuserEx-protected and skip their decryption passes (verified against de4dot-cex v4.0.0).
- Constants protection (`normal` mode): the transform between the encoded call-site argument and the decrypted-buffer ID is now a randomly generated expression per decoder (random operations, constants and depth, generated with DynCipher, emitted as IL into the decoder) instead of the fixed multiply/xor pair that generic deobfuscators extract and invert. The generation is verified to round-trip the full ID space at build time, and the call sites use the generated inverse. Previously the de4dot-cex family could locate and emulate the fixed decoders; with randomized decoders there is no stable pattern left to emulate.

## 1.2.0 — Framework probing, cross-platform GUI, sqlite-net safety (2026-10-23)

### Added
- The dependency resolver now probes the installed .NET shared frameworks (highest installed version of `Microsoft.NETCore.App`, `Microsoft.AspNetCore.App` and `Microsoft.WindowsDesktop.App`, on Windows, Linux and macOS). Platform assemblies that the runtime provides but NuGet does not copy into framework-dependent build outputs (e.g. `Microsoft.Extensions.*` on .NET 10) resolve automatically instead of failing; the module's own directory always wins over the framework copies
- The Quick Tool GUI is now a cross-platform Avalonia application (.NET 8): native UI on Windows, Linux and macOS from the same release zip (which carries the native libraries for all platforms); Linux/macOS users launch it with `dotnet Confuser.QuickTool.dll`; the obfuscation pipeline runs in-process under modern .NET (plugin discovery loads component assemblies by path)
- sqlite-net compatibility for the renamer: when an assembly references SQLite-net/sqlite-net-pcl, model classes used as generic arguments of `SQLite.*` calls (e.g. `ExecuteQuery<T>`, `Table<T>`) keep their public property and field names, since sqlite-net maps table columns to members by name (`[Column("name")]`/`[Ignore]` members remain renamable). Renaming them silently broke data binding - queries returned rows with all-null properties, surfacing as NullReferenceException far from the database code

### Changed
- Reference proxy: calls whose target cannot be resolved are left untouched instead of failing the whole protection (resolution differences against framework-provided assemblies no longer abort obfuscation)

### Fixed
- Writing debug symbols (`debug="true"`) could abort with `ModuleWriterException: Couldn't find an instruction, maybe it was removed` when a protection rebuilt a method body and left the PDB scopes referencing dropped instructions (dnlib `SerializerMethodContext.GetOffset`). The core now sanitizes the debug scopes of each module right before writing, dropping exactly the scopes with broken boundaries, so symbol output always succeeds (async state-machine methods with many nested scopes were the most exposed)

## 1.0.0 — Standalone release, automation and branding (2026-10-22)

### Added
- New ConfuserExNG icon: the CLI and the Quick Tool executables are branded with `Resource\ConfuserExNG.ico`; the Quick Tool window and taskbar use it too
- GitHub Actions CI workflow: every push/PR to master builds the release tools, verifies the required files, smoke-tests the CLI and uploads `ConfuserEx_bin.zip` as a build artifact
- Release workflow now generates release notes for tag releases; manual workflow runs update a rolling "Latest build (master)" pre-release
- README compatibility notes for Lib.Harmony: protected apps patch fine with Harmony, but self-contained single-file publishing breaks Harmony/MonoMod's clrjit detection with or without obfuscation (upstream MonoMod issue #129)

### Changed
- Standalone release packaging: `Build\Build.cmd` now builds the CLI and Quick Tool only (no legacy WPF GUI) and packs the entire `Release\bin` folder into `ConfuserEx_bin.zip`, so the archive always contains every file the tools need — including `Confuser.QuickTool.exe`, `ConfuserExNG.targets` and `ConfuserExRules.crproj` (previously missing from `files.lst`)
- GitHub Actions workflow (`.github/workflows/release.yml`) builds the release zip and attaches it to GitHub releases
- README: new "Download and use the release" section for standalone use without building from source

### Fixed
- CI: dnlib retargeted from net45 to net48 (runner images no longer ship 4.5 reference assemblies — MSB3644) and both the workflows and `Build.cmd` now restore dnlib before building (SDK-style project needs its assets on a fresh clone)

### Removed
- `Build\files.lst` (replaced by zipping `Release\bin` directly; the old list was stale and referenced files that are no longer built)

## 1.0.0-rc1 — Core Protections (2026-07-27)

### Added
- Deterministic renaming: same seed + same input = bit-identical output (b43319f)
- V1 release matrix: 3 profiles × 5 seeds × 3 samples = 45 combinations
- V1 gate verification: behavior, reproducibility, structural diversity, secrets, budget
- CI pipeline with 9 stages: build, unit-tests, build-corpus, behavior-tests, reproducibility, structural-diversity, integrity-corruption, koivm-selective, quality-gates
- Build profiles: balanced, strong, critical
- Lab evaluation pipeline: 5 deobfuscation tools, isolated execution, output inspection

### Changed
- RandomService.GetRandomGenerator clones seed array to prevent mutation across phases
- CLI `-o` mode propagates `seed` attribute from template CRPROJ
- RenamePhase sorts candidates canonically before shuffle
- ExportMapPhase writes sorted symbol map entries
- Profile budgets revised: balanced 5.5×, strong 8.5×, critical 10.0×

### Known Limitations
- Signed integrity (feat/signed-integrity) not merged
- KoiVM selective virtualization not included
- KoiVmSample not executed in this RC
- Size ratios exceed initial budgets (see release-summary.json for justification)
