[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("server", "cashier")]
    [string]$Mode,
    [Parameter(Mandatory = $true)]
    [string]$InstallRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = [IO.Path]::GetFullPath($InstallRoot)
$wizard = Join-Path $root "DeploymentWizard\POS.Deployment.Wizard.exe"

if (-not (Test-Path -LiteralPath $wizard -PathType Leaf)) {
    throw "The Advanced POS deployment wizard was not found: $wizard"
}

Start-Process -FilePath $wizard -ArgumentList @(
    "--mode", $Mode,
    "--install-root", $root
)
