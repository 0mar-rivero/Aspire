param(
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$')]
    [string] $Version = '0.1.0-beta.2',

    [string] $OutputPath = '',

    [switch] $Push,

    [string] $Source = 'https://api.nuget.org/v3/index.json',

    [string] $ApiKey = $env:NUGET_API_KEY
)

$ErrorActionPreference = 'Stop'

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$baseProject = Join-Path $repoRoot 'src\CommunityToolkit.Aspire.Hosting.Floci\CommunityToolkit.Aspire.Hosting.Floci.csproj'
$awsProject = Join-Path $repoRoot 'src\CommunityToolkit.Aspire.Hosting.Floci.AWS\CommunityToolkit.Aspire.Hosting.Floci.AWS.csproj'
$testProject = Join-Path $repoRoot 'tests\CommunityToolkit.Aspire.Hosting.Floci.AWS.Tests\CommunityToolkit.Aspire.Hosting.Floci.AWS.Tests.csproj'

if ([string]::IsNullOrWhiteSpace($OutputPath))
{
    $OutputPath = Join-Path $repoRoot 'artifacts\habichuelo-packages'
}
else
{
    $OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
}

New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null

function Invoke-DotNet
{
    param([Parameter(Mandatory)][string[]] $Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0)
    {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

Invoke-DotNet @(
    'test', $testProject,
    '--configuration', 'Release'
)

foreach ($project in @($baseProject, $awsProject))
{
    Invoke-DotNet @(
        'pack', $project,
        '--configuration', 'Release',
        '--output', $OutputPath,
        "-p:PackageVersion=$Version"
    )
}

$packages = @(
    Join-Path $OutputPath "Habichuelo.Aspire.Hosting.Floci.$Version.nupkg"
    Join-Path $OutputPath "Habichuelo.Aspire.Hosting.Floci.AWS.$Version.nupkg"
)

foreach ($package in $packages)
{
    if (-not (Test-Path -LiteralPath $package))
    {
        throw "Expected package was not created: $package"
    }
}

if ($Push)
{
    if ([string]::IsNullOrWhiteSpace($ApiKey))
    {
        throw 'Set NUGET_API_KEY or pass -ApiKey before using -Push.'
    }

    foreach ($package in $packages)
    {
        Invoke-DotNet @(
            'nuget', 'push', $package,
            '--source', $Source,
            '--api-key', $ApiKey,
            '--skip-duplicate'
        )
    }
}

Write-Output "Packages are available in $OutputPath"
