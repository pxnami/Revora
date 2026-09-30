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

The native binaries are unmodified upstream builds. Users can replace them and their DLLs with compatible builds through Settings. Review the included component licenses when redistributing; the MIT license for Revora does not replace them.

## Icons8

Navigation, refresh and copy icons use the [Icons8 Apple SF Regular family](https://icons8.com/icons/family-sf-symbols). These are Icons8-created assets, not Apple's SF Symbols files.

The free 100 px PNG assets require attribution. Revora provides a linked “Icons by Icons8” credit in Settings and this document. The applicable [Icons8 license](https://intercom.help/icons8-7fb7577e8170/en/articles/5534926-universal-multimedia-license-agreement-for-icons8) is included as `Icons8-license.html` beside the executable.

`assets/icons8.json` records the asset names and SHA-256 checksums. The build downloads originals into the ignored `bin/icons` directory and embeds them in the executable. Raw icons are not published as a separate asset pack. Icons8 assets are excluded from Revora's MIT license and retain their own terms. No Apple font is bundled; Revora uses installed Windows system fonts.
