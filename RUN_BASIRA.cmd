@echo off
setlocal
set "APP=%~dp0AIDocumentArchiveVB\bin\Release\AIDocumentArchiveVB.exe"
if not exist "%APP%" (
  echo Application not built. Run BUILD_AND_VALIDATE_AR.ps1 first.
  pause
  exit /b 1
)
pushd "%~dp0AIDocumentArchiveVB\bin\Release"
start "" "AIDocumentArchiveVB.exe"
popd
endlocal
