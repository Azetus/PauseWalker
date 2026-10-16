$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$bundleRoot = Join-Path $projectRoot 'bundle'
$destination = [System.IO.Path]::GetFullPath((Join-Path $bundleRoot 'PauseWalker'))

# Only these paths belong in the Workshop upload directory.
$requiredPaths = @('About', 'Textures', 'Sounds', 'LoadFolders.xml', 'LICENSE', 'README.md')
$versionDirectories = @('1.5', '1.6', '1.7')
$pathsToCopy = @($requiredPaths)

foreach ($relativePath in $requiredPaths) {
    if (-not (Test-Path -LiteralPath (Join-Path $projectRoot $relativePath))) {
        throw "Required bundle source is missing: $relativePath"
    }
}

foreach ($version in $versionDirectories) {
    if (Test-Path -LiteralPath (Join-Path $projectRoot $version) -PathType Container) {
        $pathsToCopy += $version
    }
    else {
        Write-Host "Skipping missing game version directory: $version"
    }
}

if ($pathsToCopy.Count -eq $requiredPaths.Count) {
    throw 'No game version directory is available to bundle.'
}

# Verify the absolute deletion target and reject redirected output directories.
$expectedParent = [System.IO.Path]::GetFullPath($bundleRoot)
if ([System.IO.Path]::GetDirectoryName($destination) -ne $expectedParent -or
    -not $destination.StartsWith($projectRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Bundle destination is outside the expected project directory: $destination"
}

foreach ($outputPath in @($bundleRoot, $destination)) {
    if (Test-Path -LiteralPath $outputPath) {
        $outputItem = Get-Item -LiteralPath $outputPath -Force
        if (-not $outputItem.PSIsContainer -or
            ($outputItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
            throw "Bundle output must be a regular directory: $outputPath"
        }
    }
}

if (Test-Path -LiteralPath $destination) {
    Remove-Item -LiteralPath $destination -Recurse -Force
}
New-Item -ItemType Directory -Path $destination -Force | Out-Null

foreach ($relativePath in $pathsToCopy) {
    Copy-Item -LiteralPath (Join-Path $projectRoot $relativePath) -Destination $destination -Recurse -Force
}

Write-Host "Mod bundle created: $destination"
