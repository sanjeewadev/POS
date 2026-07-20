[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BackupPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal $identity
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this script from Windows PowerShell as Administrator."
}

$fullPath = [IO.Path]::GetFullPath($BackupPath)
if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
    throw "SQL Server network-settings backup was not found: $fullPath"
}

$data = Get-Content -LiteralPath $fullPath -Raw | ConvertFrom-Json
$serverRoot = "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\$($data.InstanceId)\MSSQLServer"
$tcpRoot = "$serverRoot\SuperSocketNetLib\Tcp"
$ipAllRoot = "$tcpRoot\IPAll"

Set-ItemProperty -LiteralPath $serverRoot -Name LoginMode -Value ([int]$data.LoginMode)
Set-ItemProperty -LiteralPath $tcpRoot -Name Enabled -Value ([int]$data.TcpEnabled)
Set-ItemProperty -LiteralPath $tcpRoot -Name ListenOnAllIPs -Value ([int]$data.ListenOnAllIPs)
Set-ItemProperty -LiteralPath $ipAllRoot -Name TcpDynamicPorts -Value ([string]$data.TcpDynamicPorts)
Set-ItemProperty -LiteralPath $ipAllRoot -Name TcpPort -Value ([string]$data.TcpPort)

if (-not [bool]$data.FirewallRuleExisted) {
    Get-NetFirewallRule `
        -DisplayName ([string]$data.FirewallRuleName) `
        -ErrorAction SilentlyContinue |
        Remove-NetFirewallRule
}

$serviceName = [string]$data.ServiceName
$serviceRegistryPath =
    "HKLM:\SYSTEM\CurrentControlSet\Services\$serviceName"

if ($null -ne $data.PSObject.Properties["ServiceStart"]) {
    Set-ItemProperty `
        -LiteralPath $serviceRegistryPath `
        -Name Start `
        -Value ([int]$data.ServiceStart)
}

if ($null -ne $data.PSObject.Properties["FailureActionsExisted"]) {
    if ([bool]$data.FailureActionsExisted) {
        [byte[]]$failureActions =
            [Convert]::FromBase64String(
                [string]$data.FailureActionsBase64)

        New-ItemProperty `
            -Path $serviceRegistryPath `
            -Name FailureActions `
            -PropertyType Binary `
            -Value $failureActions `
            -Force | Out-Null
    }
    else {
        Remove-ItemProperty `
            -LiteralPath $serviceRegistryPath `
            -Name FailureActions `
            -ErrorAction SilentlyContinue
    }
}

$failureFlagBackupProperty =
    $data.PSObject.Properties[
        "FailureActionsOnNonCrashFailuresExisted"]

if ($null -ne $failureFlagBackupProperty) {
    if ([bool]$data.FailureActionsOnNonCrashFailuresExisted) {
        New-ItemProperty `
            -Path $serviceRegistryPath `
            -Name FailureActionsOnNonCrashFailures `
            -PropertyType DWord `
            -Value ([int]$data.FailureActionsOnNonCrashFailures) `
            -Force | Out-Null
    }
    else {
        Remove-ItemProperty `
            -LiteralPath $serviceRegistryPath `
            -Name FailureActionsOnNonCrashFailures `
            -ErrorAction SilentlyContinue
    }
}

Restart-Service -Name $serviceName -Force

$deadline = (Get-Date).AddSeconds(60)
do {
    Start-Sleep -Seconds 2
    $service = Get-Service -Name $serviceName
} while ($service.Status -ne "Running" -and (Get-Date) -lt $deadline)

if ($service.Status -ne "Running") {
    throw "SQL Server service did not return to Running state after settings restoration."
}

Write-Host "Original SQL Server network settings restored." -ForegroundColor Green
