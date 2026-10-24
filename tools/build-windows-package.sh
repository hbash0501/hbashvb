#!/usr/bin/env bash
# يبني التطبيق المكتبي ويجمعه في حزمة Windows جاهزة للتشغيل (zip).
# الاستخدام: tools/build-windows-package.sh [مجلد الإخراج]
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
out="${1:-$root/dist}"
version="$(sed -n 's/.*AssemblyFileVersion("\([0-9.]*\)").*/\1/p' "$root/AIDocumentArchiveVB/My Project/AssemblyInfo.vb")"
dotnet build "$root/tools/CompileCheck/CompileCheck.vbproj" -c Release -nologo
bin="$root/tools/CompileCheck/bin/Release/net48"
stage="$(mktemp -d)"
pkg="$stage/Basira_$version"
mkdir -p "$pkg/x86" "$pkg/x64"
cp "$bin"/AIDocumentArchiveVB.exe "$bin"/AIDocumentArchiveVB.exe.config "$bin"/AForge*.dll "$bin"/System.Data.SQLite.dll "$pkg/"
cp "$bin"/x86/SQLite.Interop.dll "$pkg/x86/"
cp "$bin"/x64/SQLite.Interop.dll "$pkg/x64/"
cp "$root/THIRD_PARTY_NOTICES.md" "$pkg/"
cp -r "$root/Licenses" "$pkg/"
printf '@echo off\r\nstart "" "%%~dp0AIDocumentArchiveVB.exe"\r\n' > "$pkg/RUN_BASIRA.cmd"
cp "$root/tools/package-readme-ar.txt" "$pkg/README_AR.txt"
mkdir -p "$out"
rm -f "$out/Basira_$version.zip"
(cd "$stage" && python3 -c "
import os, sys, zipfile
z = zipfile.ZipFile(sys.argv[1], 'w', zipfile.ZIP_DEFLATED)
for base, _, files in os.walk(sys.argv[2]):
    for f in sorted(files):
        p = os.path.join(base, f)
        z.write(p, os.path.relpath(p, os.path.dirname(sys.argv[2])))
z.close()" "$out/Basira_$version.zip" "Basira_$version")
rm -rf "$stage"
echo "$out/Basira_$version.zip"
