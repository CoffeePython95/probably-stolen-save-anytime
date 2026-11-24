param(
    [string]$GameDir = "",
    [string]$Dotnet = "",
    [switch]$Install
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$project = Join-Path $root "SaveAnytime.csproj"
$manifestPath = Join-Path $root "manifest.xml"

[xml]$manifestXml = Get-Content $manifestPath -Raw
$version = [string]$manifestXml.Manifest.ModVersion
if (-not $version) {
    throw "manifest.xml is missing <ModVersion>."
}

[xml]$projectXml = Get-Content $project -Raw
$projectVersion = [string](@($projectXml.Project.PropertyGroup.Version)[0])
if ($version -ne $projectVersion) {
    throw "Version mismatch: manifest.xml has '$version' but SaveAnytime.csproj has '$projectVersion'."
}

if (-not $GameDir) {
    if ($env:PROBABLY_STOLEN_DIR) {
        $GameDir = $env:PROBABLY_STOLEN_DIR
    } elseif (Test-Path "E:\SteamLibrary\steamapps\common\Probably Stolen") {
        $GameDir = "E:\SteamLibrary\steamapps\common\Probably Stolen"
    } elseif (Test-Path "C:\Program Files (x86)\Steam\steamapps\common\Probably Stolen") {
        $GameDir = "C:\Program Files (x86)\Steam\steamapps\common\Probably Stolen"
    } else {
        throw "Could not find Probably Stolen. Pass -GameDir `"<path to game>`" or set PROBABLY_STOLEN_DIR."
    }
}

$managed = Join-Path $GameDir "Probably Stolen_Data\Managed"
if (-not (Test-Path (Join-Path $managed "Assembly-CSharp.dll"))) {
    throw "Assembly-CSharp.dll was not found in '$managed'. Check -GameDir."
}

if (-not $Dotnet) {
    $candidates = @(
        (Join-Path $env:USERPROFILE "AppData\Local\Microsoft\dotnet\dotnet.exe"),
        (Join-Path $env:ProgramFiles "dotnet\dotnet.exe")
    )
    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            $Dotnet = $candidate
            break
        }
    }
}

if (-not $Dotnet) {
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($command) {
        $Dotnet = $command.Source
    }
}

if (-not $Dotnet -or -not (Test-Path $Dotnet)) {
    throw "dotnet was not found. Install the .NET 8 SDK or pass -Dotnet `"<path to dotnet.exe>`"."
}

Write-Host "Building Save Anytime..."
& $Dotnet build $project -c Release --nologo "-p:GameDir=$GameDir"
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE."
}

$assembly = Join-Path $root "bin\Release\netstandard2.1\SaveAnytime.dll"
if (-not (Test-Path $assembly)) {
    throw "Build succeeded but '$assembly' was not produced."
}

$dist = Join-Path $root "dist\SaveAnytime"
if (Test-Path $dist) {
    Remove-Item $dist -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $dist | Out-Null
Copy-Item $assembly $dist
Copy-Item $manifestPath $dist
if (Test-Path (Join-Path $root "README.md")) {
    Copy-Item (Join-Path $root "README.md") $dist
}
if (Test-Path (Join-Path $root "LICENSE")) {
    Copy-Item (Join-Path $root "LICENSE") $dist
}

$archive = Join-Path $root "dist\SaveAnytime-v$version.zip"
if (Test-Path $archive) {
    Remove-Item $archive -Force
}
Compress-Archive -Path $dist -DestinationPath $archive -CompressionLevel Optimal

Write-Host "Package: $dist"
Write-Host "Release archive: $archive"

if ($Install) {
    $target = Join-Path $GameDir "Mods\SaveAnytime"
    if (Test-Path $target) {
        Remove-Item $target -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $target | Out-Null
    Copy-Item (Join-Path $dist "*") $target -Recurse
    Write-Host "Installed to: $target"
    Write-Host "Enable/trust 'Save Anytime' in the Mods screen, then restart the game."
}
