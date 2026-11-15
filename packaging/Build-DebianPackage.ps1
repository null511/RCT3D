param(
	[Parameter(Mandatory = $true)][string]$PublishDirectory,
	[Parameter(Mandatory = $true)][ValidatePattern('^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$')][string]$Version,
	[Parameter(Mandatory = $true)][string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (![Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([Runtime.InteropServices.OSPlatform]::Linux)) { throw 'DEB packaging requires Ubuntu 24.04.' }
$PublishDirectory = [IO.Path]::GetFullPath($PublishDirectory)
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$packageRoot = Join-Path (Split-Path $OutputDirectory) 'debian-package'
if (Test-Path $packageRoot) { throw "Package staging directory already exists: $packageRoot" }
$payload = Join-Path $packageRoot 'opt/rct3dviewer'
$metadata = Join-Path $packageRoot 'DEBIAN'
New-Item $payload, $metadata, $OutputDirectory -ItemType Directory -Force | Out-Null
Copy-Item (Join-Path $PublishDirectory '*') $payload -Recurse
$dependencies = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
# These are loaded dynamically, so ldd alone does not capture them.
foreach ($name in 'ca-certificates', 'libicu74', 'libssl3t64', 'libgtk-3-0t64', 'libvulkan1', 'libx11-6', 'libxrandr2', 'libxinerama1', 'libxcursor1', 'libxi6', 'libwayland-client0', 'libwayland-cursor0', 'libwayland-egl1', 'libxkbcommon0') {
	[void]$dependencies.Add($name)
}
$env:LD_LIBRARY_PATH = "$payload`:$payload/runtimes/linux-x64/native"
$nativeFiles = Get-ChildItem $payload -Recurse -File | Where-Object { $_.Name -eq 'RCT3D-Viewer' -or $_.Name -match '\.so(\.|$)' }
foreach ($file in $nativeFiles) {
	$header = [IO.File]::OpenRead($file.FullName)
	try { $machine = [byte[]]::new(20); $length = $header.Read($machine, 0, 20) }
	finally { $header.Dispose() }
	if ($length -lt 20 -or [BitConverter]::ToUInt32($machine, 0) -ne 0x464c457f -or [BitConverter]::ToUInt16($machine, 18) -ne 62) {
		throw "Expected an x86-64 ELF binary: $($file.FullName)"
	}
	$linked = @(& ldd $file.FullName 2>&1)
	if ($LASTEXITCODE -ne 0 -or ($linked -match 'not found')) { throw "Unresolved dependency for $($file.Name): $linked" }
	foreach ($line in $linked) {
		if ([string]$line -notmatch '(?:=>\s+|^\s*)(/\S+)') { continue }
		$library = $Matches[1]
		if ($library.StartsWith($payload + '/', [StringComparison]::Ordinal)) { continue }
		$candidates = @($library, (& readlink -f $library)) | Select-Object -Unique
		$owner = $null
		foreach ($candidate in $candidates) {
			$owners = @(& dpkg-query -S $candidate 2>$null)
			if ($LASTEXITCODE -eq 0 -and $owners.Count -gt 0) { $owner = ([string]$owners[0] -split ': ')[0]; break }
		}
		if (!$owner) { throw "No Ubuntu package owns native dependency $library" }
		foreach ($name in ($owner -split ', ')) { [void]$dependencies.Add($name) }
	}
}
$installedSize = [Math]::Ceiling((Get-ChildItem $payload -Recurse -File | Measure-Object Length -Sum).Sum / 1024)
$control = @"
Package: rct3dviewer
Version: $Version
Section: games
Priority: optional
Architecture: amd64
Maintainer: null511 <noreply@github.com>
Installed-Size: $installedSize
Depends: $((@($dependencies) | Sort-Object) -join ', ')
Homepage: https://github.com/null511/RCT3D
Description: Experimental Vulkan 3D viewer for RollerCoaster Tycoon parks
 Bundles .NET 10. Requires Vulkan drivers and separately owned RCT1/RCT2 data.
"@
[IO.File]::WriteAllText((Join-Path $metadata 'control'), ($control -replace "`r`n", "`n") + "`n")
$launcher = Join-Path $packageRoot 'usr/bin/rct3dviewer'
New-Item (Split-Path $launcher) -ItemType Directory -Force | Out-Null
[IO.File]::WriteAllText($launcher, "#!/bin/sh`nexec /opt/rct3dviewer/RCT3D-Viewer `"`$@`"`n")
$desktop = Join-Path $packageRoot 'usr/share/applications/rct3dviewer.desktop'
New-Item (Split-Path $desktop) -ItemType Directory -Force | Out-Null
[IO.File]::WriteAllText($desktop, "[Desktop Entry]`nType=Application`nName=RCT3DViewer`nExec=rct3dviewer`nIcon=applications-games`nTerminal=false`nCategories=Game;Simulation;`n")
& chmod -R 'u=rwX,go=rX' $packageRoot
if ($LASTEXITCODE -ne 0) { throw 'Failed to set package permissions.' }
& chmod 755 $launcher (Join-Path $payload 'RCT3D-Viewer')
if ($LASTEXITCODE -ne 0) { throw 'Failed to set executable permissions.' }
$output = Join-Path $OutputDirectory "RCT3DViewer-$Version-linux-amd64.deb"
& dpkg-deb --build --root-owner-group $packageRoot $output
if ($LASTEXITCODE -ne 0) { throw 'DEB creation failed.' }
& dpkg-deb --info $output
if ($LASTEXITCODE -ne 0) { throw 'DEB metadata inspection failed.' }