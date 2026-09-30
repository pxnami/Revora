#!/usr/bin/env bash
set -euo pipefail

if [[ "${MSYSTEM:-}" != MINGW64 ]]; then
    echo 'Run this script in an MSYS2 MINGW64 shell.' >&2
    exit 1
fi

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
work=/tmp/revora-native-build
prefix=/opt/revora-native
tools="$root/dist/Revora/tools"
sources="$root/dist/native-sources"
mkdir -p "$work" "$prefix" "$tools/licenses" "$sources"
export PATH="$prefix/bin:/mingw64/bin:/usr/bin:$PATH"
export PKG_CONFIG_PATH="$prefix/lib/pkgconfig:/mingw64/lib/pkgconfig"
export CFLAGS='-O2'

while read -r project revision; do
    revision="${revision//$'\r'/}"
    checkout="$work/$project"
    if [[ ! -d "$checkout/.git" ]]; then
        git clone "https://github.com/libimobiledevice/$project.git" "$checkout"
    fi
    git -C "$checkout" fetch origin "$revision"
    git -C "$checkout" checkout --detach "$revision"
    args=("--prefix=$prefix" --disable-static)
    case "$project" in
        libplist) args+=(--without-cython --without-tests) ;;
        libimobiledevice) args+=(--without-cython --without-readline) ;;
    esac
    echo "Building $project at $revision"
    (
        cd "$checkout"
        ./autogen.sh "${args[@]}"
        make -j"${NUMBER_OF_PROCESSORS:-2}"
        make install
    )
    git -C "$checkout" archive --format=tar --prefix="$project/" "$revision" | gzip -n > "$sources/$project-$revision.tar.gz"
    mkdir -p "$tools/licenses/$project"
    find "$checkout" -maxdepth 1 -type f \( -iname 'copying*' -o -iname 'license*' -o -iname 'authors*' \) -exec cp {} "$tools/licenses/$project/" \;
done < "$root/native/revisions.txt"

declare -A copied=()
declare -A packages=()
queue=(idevice_id.exe ideviceinfo.exe ideviceenterrecovery.exe irecovery.exe idevicerestore.exe plistutil.exe)
while ((${#queue[@]})); do
    file="${queue[0]}"
    queue=("${queue[@]:1}")
    key="${file,,}"
    [[ -n "${copied[$key]:-}" ]] && continue
    copied[$key]=1
    if [[ -f "$prefix/bin/$file" ]]; then
        origin="$prefix/bin/$file"
    elif [[ -f "/mingw64/bin/$file" ]]; then
        origin="/mingw64/bin/$file"
        package="$(pacman -Qqo "$origin")"
        packages[$package]=1
    elif [[ -f "$(cygpath -u "$WINDIR")/System32/$file" || "$key" == api-ms-* || "$key" == ext-ms-* ]]; then
        continue
    else
        echo "Unresolved native dependency: $file" >&2
        exit 1
    fi
    cp "$origin" "$tools/$file"
    while read -r dependency; do
        [[ -n "$dependency" ]] && queue+=("$dependency")
    done < <(objdump -p "$origin" | sed -n 's/^[[:space:]]*DLL Name: //p' | tr -d '\r')
done

: > "$tools/package-versions.txt"
for package in "${!packages[@]}"; do
    read -r name version <<< "$(pacman -Q "$package")"
    echo "$name $version" >> "$tools/package-versions.txt"
    while read -r license; do
        if [[ -f "$license" ]]; then
            relative="${license#/mingw64/share/licenses/}"
            mkdir -p "$tools/licenses/$(dirname "$relative")"
            cp "$license" "$tools/licenses/$relative"
        fi
    done < <(pacman -Ql "$package" | awk '$2 ~ /^\/mingw64\/share\/licenses\// { print $2 }')
    base="$(awk '/^%BASE%$/ { getline; print; exit }' "/var/lib/pacman/local/$name-$version/desc")"
    [[ -n "$base" ]] || { echo "Missing source package metadata for $name" >&2; exit 1; }
    if [[ ! -f "$sources/$base-$version.src.tar.zst" ]]; then
        curl --fail --location --silent --show-error "https://mirror.msys2.org/mingw/sources/$base-$version.src.tar.zst" -o "$sources/$base-$version.src.tar.zst"
    fi
done
sort -o "$tools/package-versions.txt" "$tools/package-versions.txt"
cp "$root/native/revisions.txt" "$tools/upstream-revisions.txt"
cp "$root/scripts/build-native.sh" "$sources/"
cp "$root/native/revisions.txt" "$sources/"
echo 'Native tools built and bundled. Checking executable dependencies…'
"$tools/idevice_id.exe" --help >/dev/null
"$tools/ideviceinfo.exe" --help >/dev/null
"$tools/ideviceenterrecovery.exe" --help >/dev/null
"$tools/irecovery.exe" --help >/dev/null
"$tools/idevicerestore.exe" --help >/dev/null
"$tools/plistutil.exe" --help >/dev/null
echo "Ready: $tools"
