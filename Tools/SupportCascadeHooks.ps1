param([Parameter(Mandatory = $true)][string]$Phase, [Parameter(Mandatory = $true)][string]$Version)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$about = Join-Path $repository 'About\About.xml'
$xml = @"
<?xml version="1.0" encoding="utf-8"?>
<ModMetaData>
  <name>Caravan Readiness</name>
  <author>CoolNether123</author>
  <packageId>CoolNether123.CaravanReadiness</packageId>
  <modVersion>1.0.0</modVersion>
  <supportedVersions><li>$Version</li></supportedVersions>
  <modDependencies>
    <li><packageId>brrainz.harmony</packageId><displayName>Harmony</displayName></li>
    <li><packageId>CoolNether123.Spine</packageId><displayName>SpineLib</displayName></li>
  </modDependencies>
  <loadAfter><li>brrainz.harmony</li><li>CoolNether123.Spine</li></loadAfter>
  <description>Live caravan formation readiness report for RimWorld $Version. Requires Harmony and SpineLib.</description>
</ModMetaData>
"@
$loadFolders = "<?xml version=`"1.0`" encoding=`"utf-8`"?><loadFolders><v$Version><li>/</li><li>$Version</li></v$Version></loadFolders>"
if ($Phase -eq 'after-merge') {
    [System.IO.File]::WriteAllText($about, $xml)
    [System.IO.File]::WriteAllText((Join-Path $repository 'LoadFolders.xml'), $loadFolders)
    & git -C $repository add -- About/About.xml LoadFolders.xml
    if ($LASTEXITCODE -ne 0) { throw 'Could not stage support metadata.' }
}
elseif ($Phase -eq 'before-stage') {
    $source = Join-Path $repository "$Version\Assemblies\CaravanReadiness.dll"
    $destinationRoot = Join-Path $repository 'Assemblies'
    [System.IO.Directory]::CreateDirectory($destinationRoot) | Out-Null
    [System.IO.File]::Copy($source, (Join-Path $destinationRoot 'CaravanReadiness.dll'), $true)
    & git -C $repository add -- Assemblies About/About.xml LoadFolders.xml
    if ($LASTEXITCODE -ne 0) { throw 'Could not stage support payload.' }
}
