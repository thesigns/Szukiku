# Publikuje szukiku.exe jako Native AOT do katalogu .\publish
# vcvarsall.bat z VS 2026 woła vswhere.exe przez PATH, a instalator VS go tam nie dodaje — stąd dopisanie katalogu Installer.
$installer = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer"
if (Test-Path $installer) { $env:PATH = "$installer;$env:PATH" }

dotnet publish "$PSScriptRoot\src\Szukiku.Cli" -c Release -r win-x64 -o "$PSScriptRoot\publish" -nologo
