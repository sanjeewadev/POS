[CmdletBinding()]
param(
    [string]$InstanceName = "SQLEXPRESS",
    [ValidateRange(1, 65535)]
    [int]$Port = 1433,
    [string]$BackupPath = "",
    [switch]$SetActiveNetworkPrivate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal $identity
    $isAdministrator = $principal.IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)

    if (-not $isAdministrator) {
        throw "Run this script from Windows PowerShell as Administrator."
    }
}

function Get-SqlInstanceId {
    param([string]$Name)

    $path = "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL"
    if (-not (Test-Path -LiteralPath $path)) {
        throw "SQL Server instance registry key was not found."
    }

    $properties = Get-ItemProperty -LiteralPath $path
    $property = $properties.PSObject.Properties[$Name]
    if ($null -eq $property -or [string]::IsNullOrWhiteSpace([string]$property.Value)) {
        throw "SQL Server instance '$Name' was not found."
    }

    return [string]$property.Value
}

function Write-RegistryBackup {
    param(
        [string]$Path,
        [string]$InstanceId,
        [string]$ServerRoot,
        [string]$TcpRoot,
        [string]$IpAllRoot,
        [string]$ServiceName,
        [string]$FirewallRuleName
    )

    $server = Get-ItemProperty -LiteralPath $ServerRoot
    $tcp = Get-ItemProperty -LiteralPath $TcpRoot
    $ipAll = Get-ItemProperty -LiteralPath $IpAllRoot
    $firewallExists = $null -ne (Get-NetFirewallRule `
        -DisplayName $FirewallRuleName `
        -ErrorAction SilentlyContinue)

    $data = [ordered]@{
        GeneratedAt = (Get-Date).ToString("o")
        InstanceName = $InstanceName
        InstanceId = $InstanceId
        ServiceName = $ServiceName
        LoginMode = [int]$server.LoginMode
        TcpEnabled = [int]$tcp.Enabled
        ListenOnAllIPs = [int]$tcp.ListenOnAllIPs
        TcpDynamicPorts = [string]$ipAll.TcpDynamicPorts
        TcpPort = [string]$ipAll.TcpPort
        FirewallRuleName = $FirewallRuleName
        FirewallRuleExisted = $firewallExists
    }

    $folder = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($folder)) {
        New-Item -ItemType Directory -Path $folder -Force | Out-Null
    }

    $data | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $Path -Encoding UTF8
}

Assert-Administrator

$instanceId = Get-SqlInstanceId -Name $InstanceName
$serviceName = "MSSQL`$$InstanceName"
$serverRoot = "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\$instanceId\MSSQLServer"
$tcpRoot = "$serverRoot\SuperSocketNetLib\Tcp"
$ipAllRoot = "$tcpRoot\IPAll"
$firewallRuleName = "POS SQL Server TCP $Port"

foreach ($requiredPath in @($serverRoot, $tcpRoot, $ipAllRoot)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required SQL Server registry path was not found: $requiredPath"
    }
}

if ([string]::IsNullOrWhiteSpace($BackupPath)) {
    $desktop = [Environment]::GetFolderPath("Desktop")
    $BackupPath = Join-Path $desktop (
        "POS_SQLServer_Network_Settings_Backup_" +
        (Get-Date -Format "yyyyMMdd_HHmmss") +
        ".json")
}
else {
    $BackupPath = [IO.Path]::GetFullPath($BackupPath)
}

Write-RegistryBackup `
    -Path $BackupPath `
    -InstanceId $instanceId `
    -ServerRoot $serverRoot `
    -TcpRoot $tcpRoot `
    -IpAllRoot $ipAllRoot `
    -ServiceName $serviceName `
    -FirewallRuleName $firewallRuleName

try {
    $existingRules = @(Get-NetFirewallRule `
        -DisplayName $firewallRuleName `
        -ErrorAction SilentlyContinue)

    if ($existingRules.Count -gt 1) {
        throw "More than one firewall rule is named '$firewallRuleName'. No firewall rule was changed."
    }

    if ($existingRules.Count -eq 1) {
        $existingRule = $existingRules[0]
        $portFilters = @($existingRule | Get-NetFirewallPortFilter)
        $validRule =
            $existingRule.Direction -eq "Inbound" -and
            $existingRule.Action -eq "Allow" -and
            $existingRule.Enabled -eq "True" -and
            ([string]$existingRule.Profile).IndexOf(
                "Private",
                [StringComparison]::OrdinalIgnoreCase) -ge 0 -and
            $portFilters.Count -eq 1 -and
            ([string]$portFilters[0].Protocol -eq "TCP" -or
             [string]$portFilters[0].Protocol -eq "6") -and
            [string]$portFilters[0].LocalPort -eq [string]$Port

        if (-not $validRule) {
            throw "An existing firewall rule named '$firewallRuleName' does not match the required Private inbound TCP configuration. No firewall rule was changed."
        }
    }

    Set-ItemProperty -LiteralPath $serverRoot -Name LoginMode -Value 2
    Set-ItemProperty -LiteralPath $tcpRoot -Name Enabled -Value 1
    Set-ItemProperty -LiteralPath $tcpRoot -Name ListenOnAllIPs -Value 1
    Set-ItemProperty -LiteralPath $ipAllRoot -Name TcpDynamicPorts -Value ""
    Set-ItemProperty -LiteralPath $ipAllRoot -Name TcpPort -Value ([string]$Port)

    if ($existingRules.Count -eq 0) {
        New-NetFirewallRule `
            -DisplayName $firewallRuleName `
            -Direction Inbound `
            -Action Allow `
            -Protocol TCP `
            -LocalPort $Port `
            -Profile Private `
            -Enabled True | Out-Null
    }

    if ($SetActiveNetworkPrivate) {
        $profiles = @(Get-NetConnectionProfile | Where-Object {
            $_.IPv4Connectivity -ne "Disconnected" -and
            $_.NetworkCategory -ne "DomainAuthenticated"
        })

        foreach ($profile in $profiles) {
            Set-NetConnectionProfile `
                -InterfaceIndex $profile.InterfaceIndex `
                -NetworkCategory Private
        }
    }

    Restart-Service -Name $serviceName -Force

    $deadline = (Get-Date).AddSeconds(60)
    do {
        Start-Sleep -Seconds 2
        $service = Get-Service -Name $serviceName
    } while ($service.Status -ne "Running" -and (Get-Date) -lt $deadline)

    if ($service.Status -ne "Running") {
        throw "SQL Server service did not return to Running state."
    }

    $tcpReady = $false
    $deadline = (Get-Date).AddSeconds(60)
    do {
        Start-Sleep -Seconds 2
        $test = Test-NetConnection `
            -ComputerName 127.0.0.1 `
            -Port $Port `
            -WarningAction SilentlyContinue
        $tcpReady = [bool]$test.TcpTestSucceeded
    } while (-not $tcpReady -and (Get-Date) -lt $deadline)

    if (-not $tcpReady) {
        throw "SQL Server did not begin listening on TCP port $Port."
    }
}
catch {
    $configurationError = $_
    $restoreScript = Join-Path $PSScriptRoot "Restore-POS-SqlServer-Network.ps1"
    try {
        & powershell.exe `
            -NoProfile `
            -ExecutionPolicy Bypass `
            -File $restoreScript `
            -BackupPath $BackupPath | Out-Host
    }
    catch {
        Write-Host "Automatic restoration also failed. Restore manually from: $BackupPath" -ForegroundColor Yellow
    }

    throw $configurationError
}

Write-Host "POS SQL Server network configuration completed." -ForegroundColor Green
Write-Host "Instance:  $InstanceName"
Write-Host "TCP port:  $Port"
Write-Host "Firewall:  $firewallRuleName (Private networks only)"
Write-Host "Backup:    $BackupPath"
