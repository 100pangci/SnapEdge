$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot 'src\SnapEdge.App\SnapEdge.App.csproj'
$releaseDirectory = Join-Path $repositoryRoot 'artifacts\release'
$releaseExe = Join-Path $releaseDirectory 'SnapEdge.exe'

$dotnetExe = $null
$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
if ($dotnetCommand) {
    $candidate = $dotnetCommand.Source
    $installedSdks = & $candidate --list-sdks 2>$null
    if ($installedSdks -match '^10\.') {
        $dotnetExe = $candidate
    }
}

if (-not $dotnetExe) {
    $sdkPath = 'D:\SDK\DotNet\dotnet-sdk-10.0.403\dotnet.exe'
    if (Test-Path -LiteralPath $sdkPath) {
        $dotnetExe = $sdkPath
    }
}

if (-not $dotnetExe) {
    throw 'SnapEdge requires the .NET 10 SDK. Install it or make dotnet 10 available on PATH.'
}

if (Test-Path -LiteralPath 'D:\SDK\NuGet') {
    $env:NUGET_PACKAGES = 'D:\SDK\NuGet'
}

$temporaryPublishDirectory = Join-Path $env:TEMP ('SnapEdge-publish-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporaryPublishDirectory | Out-Null

try {
    & $dotnetExe publish $projectPath `
        --configuration Release `
        --runtime win-x64 `
        --self-contained false `
        --output $temporaryPublishDirectory `
        --verbosity minimal `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=false `
        -p:PublishTrimmed=false `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -p:EnableCompressionInSingleFile=true

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }

    $publishedFiles = @(Get-ChildItem -LiteralPath $temporaryPublishDirectory -File)
    $unexpectedFiles = @($publishedFiles | Where-Object { $_.Name -ne 'SnapEdge.exe' })
    if ($unexpectedFiles.Count -gt 0 -or -not (Test-Path -LiteralPath (Join-Path $temporaryPublishDirectory 'SnapEdge.exe'))) {
        $names = ($publishedFiles | ForEach-Object Name) -join ', '
        throw "The publish output was not a single executable. Files found: $names"
    }

    New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $temporaryPublishDirectory 'SnapEdge.exe') -Destination $releaseExe -Force
    Write-Output "Release executable: $releaseExe"
}
finally {
    if (Test-Path -LiteralPath $temporaryPublishDirectory) {
        Remove-Item -LiteralPath $temporaryPublishDirectory -Recurse -Force -Confirm:$false
    }
}
