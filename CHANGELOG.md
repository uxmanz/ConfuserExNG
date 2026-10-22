# Changelog

## Unreleased

### Added
- GitHub Actions CI workflow: every push/PR to master builds the release tools, verifies the required files, smoke-tests the CLI and uploads `ConfuserEx_bin.zip` as a build artifact
- Release workflow now generates release notes for tag releases
- README compatibility notes for Lib.Harmony: protected apps patch fine with Harmony, but self-contained single-file publishing breaks Harmony/MonoMod's clrjit detection with or without obfuscation (upstream MonoMod issue #129)

### Changed
- Standalone release packaging: `Build\Build.cmd` now builds the CLI and Quick Tool only (no legacy WPF GUI) and packs the entire `Release\bin` folder into `ConfuserEx_bin.zip`, so the archive always contains every file the tools need — including `Confuser.QuickTool.exe`, `ConfuserExNG.targets` and `ConfuserExRules.crproj` (previously missing from `files.lst`)
- GitHub Actions workflow (`.github/workflows/release.yml`) builds the release zip and attaches it to GitHub releases
- README: new "Download and use the release" section for standalone use without building from source

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
