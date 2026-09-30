# Native components

Revora invokes native executables as separate processes. It does not contain proprietary ReiBoot code, Apple firmware, or Apple USB drivers.

Core sources are pinned in `native/revisions.txt`:

- [libplist](https://github.com/libimobiledevice/libplist)
- [libimobiledevice-glue](https://github.com/libimobiledevice/libimobiledevice-glue)
- [libusbmuxd](https://github.com/libimobiledevice/libusbmuxd)
- [libtatsu](https://github.com/libimobiledevice/libtatsu)
- [libimobiledevice](https://github.com/libimobiledevice/libimobiledevice)
- [libirecovery](https://github.com/libimobiledevice/libirecovery)
- [idevicerestore](https://github.com/libimobiledevice/idevicerestore)

The build copies each project's license and author files to `tools/licenses`. DLL dependencies come from official MSYS2 packages; their exact versions are recorded in `tools/package-versions.txt`, with available license files in `tools/licenses`. Corresponding source archives are distributed alongside binary releases in `Revora-native-sources.zip`, including the pinned upstream sources and MSYS2 source packages.

The native binaries are unmodified upstream builds. Users can replace them and their DLLs with compatible builds through the Setup tools folder. Review the included component licenses when redistributing; the MIT license for Revora does not replace them.
