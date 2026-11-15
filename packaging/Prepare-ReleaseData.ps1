param(
	[Parameter(Mandatory = $true)][string]$OutputDirectory,
	[string]$DownloadDirectory = (Join-Path $PSScriptRoot '..\artifacts\downloads')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot
$nativeRoot = Join-Path $root 'OpenRCT2'
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$DownloadDirectory = [IO.Path]::GetFullPath($DownloadDirectory)
New-Item $OutputDirectory, $DownloadDirectory -ItemType Directory -Force | Out-Null

function Get-VerifiedArchive([string]$Url, [string]$Sha256, [string]$Name) {
	$archive = Join-Path $DownloadDirectory $Name
	if (!(Test-Path $archive) -or (Get-FileHash $archive -Algorithm SHA256).Hash -ne $Sha256) {
		Invoke-WebRequest $Url -OutFile "$archive.download" -UseBasicParsing
		if ((Get-FileHash "$archive.download" -Algorithm SHA256).Hash -ne $Sha256) {
			throw "SHA-256 mismatch for $Url"
		}
		Move-Item "$archive.download" $archive -Force
	}
	return $archive
}

$assets = Get-Content (Join-Path $nativeRoot 'assets.json') -Raw | ConvertFrom-Json
foreach ($entry in @(@{ Key = 'objects'; Destination = 'object' }, @{ Key = 'title-sequences'; Destination = 'sequence' })) {
	$asset = $assets.($entry.Key)
	$archive = Get-VerifiedArchive $asset.url $asset.sha256 ($entry.Key + '.zip')
	Expand-Archive $archive (Join-Path $OutputDirectory $entry.Destination) -Force
}

# Use the released sprite compiler against this fork's manifests, without building any CLI/GUI target.
$toolHash = '6c07393312a828f845c71c9c18cefe2da5b759fe2b17ee02fbb9e4540b396280'
$toolArchive = Get-VerifiedArchive 'https://github.com/OpenRCT2/OpenRCT2/releases/download/v0.5.6/OpenRCT2-v0.5.6-windows-portable-x64.zip' $toolHash 'openrct2-sprite-tool.zip'
$toolDirectory = Join-Path $DownloadDirectory 'openrct2-sprite-tool'
Expand-Archive $toolArchive $toolDirectory -Force
$tools = @(Get-ChildItem $toolDirectory -Filter 'openrct2.exe' -Recurse)
if ($tools.Count -ne 1) { throw 'Expected one OpenRCT2 sprite compiler executable.' }
foreach ($name in 'g2', 'fonts', 'palettes', 'tracks') {
	$output = Join-Path $OutputDirectory "$name.dat"
	& $tools[0].FullName sprite build $output (Join-Path $nativeRoot "resources\$name\sprites.json") silent
	if ($LASTEXITCODE -ne 0) { throw "Sprite compilation failed: $name" }
	if (!(Test-Path $output) -or (Get-Item $output).Length -le 8) { throw "Missing sprite output: $output" }
}
if (@(Get-ChildItem (Join-Path $OutputDirectory 'object') -Recurse -File).Count -eq 0) { throw 'Object archive was empty.' }
Copy-Item (Join-Path $nativeRoot 'assets.json') (Join-Path $OutputDirectory 'asset-sources.json') -Force