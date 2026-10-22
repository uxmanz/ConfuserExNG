# Building ConfuserEx

## Prerequisites

- Visual Studio 2022 Community (or later)
- .NET Framework 4.8 targeting pack
- MSBuild 17.x (included with VS 2022)

## Build

```powershell
$msbuild = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe"
$tfr = "D:\dev\confuser-lab\tooling\reference-assemblies"

& $msbuild .\Confuser2.sln /m /t:Build /p:Configuration=Release "/p:TargetFrameworkRootPath=$tfr"
```

The built binaries are placed in `Release\bin\`.

### Release zip

`Build\Build.cmd` builds the shippable tools (CLI + Quick Tool), checks that
every required file exists in `Release\bin`, and packs the folder contents
into `Build\ConfuserEx_bin.zip`. The archive matches `Release\bin` exactly,
so the extracted folder is a complete standalone release — see the
*Download and use the release* section in the README.

## Running

```powershell
.\Release\bin\Confuser.CLI.exe -n -o <output-dir> <input.exe> <profile.crproj>
```

### Deterministic builds

Add a `seed` attribute to your `.crproj` file:

```xml
<project outputDir="..\Release" baseDir="." seed="my-stable-seed" xmlns="...">
```

Then use the `-o` flag to specify an explicit output directory.
