param(
    [switch]$NoRestore
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

New-Item -ItemType Directory -Force -Path ".appdata\NuGet" | Out-Null
Copy-Item ".\NuGet.Config" ".\.appdata\NuGet\NuGet.Config" -Force

$env:APPDATA = "$root\.appdata"
$env:DOTNET_CLI_HOME = $root
$env:NUGET_PACKAGES = "$root\.nuget\packages"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_NOLOGO = "1"

if (-not $NoRestore) {
    dotnet restore ".\VirgoHub.WebAdmin\VirgoHub.WebAdmin.csproj" --configfile ".\.appdata\NuGet\NuGet.Config"
}

$port = 5128
$localIp = (
    Get-NetIPAddress -AddressFamily IPv4 |
    Where-Object {
        $_.IPAddress -notlike "127.*" -and
        $_.IPAddress -notlike "169.254.*" -and
        $_.PrefixOrigin -ne "WellKnown"
    } |
    Sort-Object InterfaceMetric |
    Select-Object -First 1 -ExpandProperty IPAddress
)

Write-Host ""
Write-Host "VirgoHub Web Admin starting..." -ForegroundColor Cyan
Write-Host "Local portal:    http://localhost:$port" -ForegroundColor White
if (-not [string]::IsNullOrWhiteSpace($localIp)) {
    Write-Host "Mobile portal:   http://$localIp`:$port" -ForegroundColor Green
    Write-Host "Use that Mobile portal address inside the VirgoHub app." -ForegroundColor DarkGray
}
Write-Host ""

dotnet run --project ".\VirgoHub.WebAdmin\VirgoHub.WebAdmin.csproj" --urls "http://0.0.0.0:$port"
