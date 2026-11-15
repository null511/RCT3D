param(
	[Parameter(Mandatory = $true)][string]$PublishDirectory,
	[Parameter(Mandatory = $true)][ValidateSet('win-x64', 'linux-x64')][string]$RuntimeIdentifier
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot
$PublishDirectory = [IO.Path]::GetFullPath($PublishDirectory)
$library = 'libopenrct2.so'
$executable = 'RCT3D-Viewer'
if ($RuntimeIdentifier -eq 'win-x64') {
	$library = 'libopenrct2.dll'
	$executable += '.exe'
}
foreach ($path in @($executable, $library, 'RCT3D-Viewer.runtimeconfig.json', 'System.Private.CoreLib.dll', 'Assets', 'Rides\Vehicles', 'openrct2-data\object', 'openrct2-data\language', 'openrct2-data\sequence', 'openrct2-data\asset-sources.json', 'openrct2-data\g2.dat', 'openrct2-data\fonts.dat', 'openrct2-data\palettes.dat', 'openrct2-data\tracks.dat')) {
	if (!(Test-Path (Join-Path $PublishDirectory $path))) { throw "Publish output is missing $path" }
}
$shaders = @(Get-ChildItem (Join-Path $root 'RCT3DViewer\Shaders') -Filter '*.slang' -Recurse)
foreach ($shader in $shaders) {
	$text = Get-Content $shader.FullName -Raw
	if ($text -match '(?m)^\s*(public\s+)?module\s') { continue }
	$relative = [IO.Path]::GetRelativePath((Join-Path $root 'RCT3DViewer\Shaders'), $shader.FullName)
	$stages = @('vert', 'frag')
	if ($text -match '\[shader\("compute"\s*\)\]') { $stages = @('comp') }
	foreach ($stage in $stages) {
		$path = Join-Path $PublishDirectory ('Shaders\' + [IO.Path]::ChangeExtension($relative, "$stage.spv"))
		if (!(Test-Path $path) -or (Get-Item $path).Length -lt 4) { throw "Missing compiled shader $path" }
		$bytes = [IO.File]::ReadAllBytes($path)
		if ([BitConverter]::ToUInt32($bytes, 0) -ne 0x07230203) { throw "Invalid SPIR-V: $path" }
	}
}

$licenses = Join-Path $PublishDirectory 'licenses'
New-Item $licenses -ItemType Directory -Force | Out-Null
Copy-Item (Join-Path $root 'OpenRCT2\licence.txt') (Join-Path $licenses 'OpenRCT2-GPL-3.0.txt') -Force
Copy-Item (Join-Path $PSScriptRoot 'THIRD-PARTY-NOTICES.txt') $licenses -Force
$assets = Get-Content (Join-Path $root 'RCT3DViewer\obj\project.assets.json') -Raw | ConvertFrom-Json
$packageRoot = @($assets.packageFolders.PSObject.Properties)[0].Name
foreach ($package in $assets.libraries.PSObject.Properties) {
	if ($package.Value.type -ne 'package') { continue }
	$directory = Join-Path $packageRoot $package.Value.path
	$destination = Join-Path $licenses ($package.Name -replace '/', '-')
	New-Item $destination -ItemType Directory -Force | Out-Null
	Get-ChildItem $directory -File | Where-Object { $_.Name -match '(?i)license|licence|notice|\.nuspec$' } | Copy-Item -Destination $destination -Force
}
if ($RuntimeIdentifier -eq 'win-x64') {
	$nativeNotices = Get-ChildItem (Join-Path $root 'OpenRCT2\lib\x64\share') -Recurse -File | Where-Object { $_.Name -match '(?i)^copyright$|license|licence|notice' }
	foreach ($notice in $nativeNotices) {
		$relative = [IO.Path]::GetRelativePath((Join-Path $root 'OpenRCT2\lib\x64\share'), $notice.FullName)
		$destination = Join-Path $licenses ('native\' + $relative)
		New-Item (Split-Path $destination) -ItemType Directory -Force | Out-Null
		Copy-Item $notice.FullName $destination -Force
	}
	Copy-Item (Join-Path $root 'OpenRCT2\lib\x64\licenses') (Join-Path $licenses 'native-extra') -Recurse -Force
}
Get-ChildItem $PublishDirectory -Recurse -File | Where-Object { $_.Extension -in @('.pdb', '.exp', '.lib') } | Remove-Item