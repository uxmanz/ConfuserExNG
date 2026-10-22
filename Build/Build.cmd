@echo off
setlocal
cd /d "%~dp0"

rem Locate MSBuild: prefer Visual Studio / Build Tools, fall back to the
rem .NET Framework toolchain.
set "MSBUILD=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe"
if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" (
    for /f "usebackq delims=" %%i in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe 2^>nul`) do set "MSBUILD=%%i"
)

rem Only the shippable tools are built: the CLI pulls in all component
rem assemblies (Core, Protections, Renamer, DynCipher, Runtime, dnlib) and the
rem Quick Tool is the GUI. The legacy WPF GUI (ConfuserEx) is built separately
rem and is not part of the release zip.
"%MSBUILD%" ..\Confuser.CLI\Confuser.CLI.csproj /p:Configuration=Release /p:Platform=AnyCPU /v:minimal /nologo
IF %ERRORLEVEL% NEQ 0 GOTO err
"%MSBUILD%" ..\Confuser.QuickTool\Confuser.QuickTool.csproj /p:Configuration=Release /p:Platform=AnyCPU /v:minimal /nologo
IF %ERRORLEVEL% NEQ 0 GOTO err

rem The zip is the entire Release\bin folder, so the archive always matches
rem what the tools need at runtime: everything (CLI, Quick Tool, component
rem assemblies, ConfuserExNG.targets, ConfuserExRules.crproj) ships together
rem and the extracted folder works standalone on any computer.
cd /d "%~dp0..\Release\bin"
FOR %%F IN (Confuser.CLI.exe Confuser.Core.dll Confuser.Protections.dll Confuser.Renamer.dll Confuser.DynCipher.dll Confuser.Runtime.dll dnlib.dll Confuser.QuickTool.exe ConfuserExNG.targets ConfuserExRules.crproj) DO (
    IF NOT EXIST "%%F" (
        ECHO Missing required release file: %%F
        GOTO err
    )
)
IF EXIST "%~dp0ConfuserEx_bin.zip" DEL "%~dp0ConfuserEx_bin.zip"
"%~dp07z.exe" a "%~dp0ConfuserEx_bin.zip" -tzip * -xr!confex-tmp -x!*.confex-stamp >nul
IF %ERRORLEVEL% NEQ 0 GOTO err
ECHO Created %~dp0ConfuserEx_bin.zip from Release\bin
EXIT /B 0

:err
PAUSE
EXIT /B 1
