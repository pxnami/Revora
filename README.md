# Revora

Windows desktop utility for iPhone and iPad recovery mode and IPSW firmware installs. Built with C# and Windows Forms; native device communication uses the libimobiledevice tools.

**Early development release. Real-device recovery and restore are not yet validated. Back up before installing firmware.**

## Features

- USB device detection, device information, and automatic refresh.
- Enter recovery mode and request a return to normal mode.
- Recognize DFU devices; exit DFU with a manual force restart.
- Select a local IPSW, inspect its manifest, and check product, hardware, and install variant compatibility.
- Update installs that attempt to preserve data, or erase restores with explicit confirmation.
- ECID-targeted restore with a fresh identity check before starting.
- Native restore stage progress and local activity logs.

Revora cannot guarantee data preservation, repair hardware, bypass Activation Lock, or restore firmware Apple no longer signs. Exit recovery requests a reboot; an underlying boot problem can return the device to recovery.

## Run

Requires Windows 10/11 x64, .NET Framework 4.8, and Apple's USB drivers (install [Apple Devices](https://support.apple.com/guide/devices-windows/welcome/windows)).

Extract the complete `Revora-windows-x64.zip` and run `Revora.exe`. Keep the `tools` folder next to the executable. Unlock a normal-mode device and accept **Trust This Computer**. Connect one recovery / DFU device at a time; the recovery CLI discovers one device per scan.

For firmware installs, choose an Apple IPSW that matches the device. Revora checks local compatibility; the restore engine checks signing with Apple during the install. An update requires an Update install variant in the IPSW. Erase restore deletes all data and does not remove the device's Apple Account association.

Logs and extracted firmware cache are stored in `%LOCALAPPDATA%\Revora`. Logs may contain device identifiers; review them before sharing. Open the folder from Setup. You can delete cached files after an operation has finished to reclaim disk space.

## Build

The desktop app builds with the C# compiler included in Windows .NET Framework. No NuGet packages or SDK install are required.

```powershell
./scripts/build.ps1 -Test
```

This creates `dist/Revora/Revora.exe`. For a complete distributable, install [MSYS2](https://www.msys2.org/), open its **MINGW64** shell, and install build dependencies:

```bash
pacman -Syu
pacman -S --needed base-devel git mingw-w64-x86_64-gcc mingw-w64-x86_64-autotools mingw-w64-x86_64-pkgconf mingw-w64-x86_64-openssl mingw-w64-x86_64-curl mingw-w64-x86_64-libzip mingw-w64-x86_64-readline
bash scripts/build-native.sh
```

The native script builds the revisions in `native/revisions.txt`, copies the required DLLs, includes upstream license files, and collects corresponding sources. It uses `/tmp/revora-native-build` and `/opt/revora-native` inside MSYS2. Native build dependencies come from the current MSYS2 registry; exact package versions are recorded in the bundle.

Then in PowerShell:

```powershell
./scripts/build.ps1 -Test -Package
Compress-Archive -Path dist/native-sources/* -DestinationPath dist/Revora-native-sources.zip
```

Distribute both the Windows package and native sources archive. GitHub Actions performs these steps on pushes and pull requests. Tags beginning with `v` create a GitHub prerelease with both archives and a checksum. Releases are unsigned; code signing requires your own Windows signing certificate.

## Validation

`scripts/build.ps1 -Test` checks device parsing, firmware compatibility, target selection, restore argument safety, mismatched device rejection, process quoting, stream handling, and tool timeouts. These tests use synthetic fixtures and do not touch connected devices.

Before recommending a release for real use, follow [the device validation checklist](docs/DEVICE-VALIDATION.md).

## License

Revora's C# source is MIT licensed. Bundled native tools and dependencies retain their own licenses; see [THIRD-PARTY.md](THIRD-PARTY.md).
