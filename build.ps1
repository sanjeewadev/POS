# ===================================================================
# Advanced POS - Build & Staging Script (v2)
# ===================================================================

# --- CONFIGURATION ---
# You only need to edit this section.

# 1. Set your application version.
$appVersion = "1.0.8"

# 2. (Optional) Set a branch name for pre-release builds. Leave empty for a final release.
#    This will create a setup file like: Advanced-POS-Setup-1.0.8-my-feature.exe
$branchName = "" # e.g., "feature-new-reports"

# 3. Verify the path to the Inno Setup compiler.
#    This is NOT the SQL Server installer. You must install Inno Setup 6.
#    The path below is the default for Inno Setup 6.
$innoSetupCompilerPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

# 4. (For your reference) Path to your SQL Server Express installer.
#    This is a prerequisite for your app and is NOT used by this build script.
#    You would typically bundle this inside your Inno Setup script if needed.
$sqlServerInstallerPath = "C:\Users\Sanjeewa\Desktop\Advance_POS\SQLServer2022Express\SQLEXPR_x64_ENU.exe"


# --- SCRIPT ---
# (You generally won't need to change anything below this line)

# --- 1. Prepare Version and Paths ---
Write-Host "Starting Advanced POS build process..." -ForegroundColor Cyan

# Construct the full version string for filenames and assembly info.
$fullVersionString = $appVersion
if (-not [string]::IsNullOrWhiteSpace($branchName)) {
    # Sanitize branch name for use in filenames (removes spaces and special characters)
    $sanitizedBranch = $branchName -replace '[^a-zA-Z0-9-]', '-'
    $fullVersionString = "$appVersion-$sanitizedBranch"
}
Write-Host "Building version: $fullVersionString"

$solutionFile = "POS.sln"
$stagingDir = ".\dist"
$fullSetupScript = ".\setup.iss"
$cashierSetupScript = ".\setup-cashier.iss"

# --- 2. Clean and Publish Projects ---
Write-Host "Cleaning the solution..."
dotnet clean $solutionFile --configuration Release

# Define all the applications that need to be included in the setup
$projectsToPublish = @(
    "POS.BackOffice.UI\POS.BackOffice.UI.csproj",
    "POS.Cashier.UI\POS.Cashier.UI.csproj",
    "POS.Deployment.Wizard\POS.Deployment.Wizard.csproj",
    "POS.Database.Setup\POS.Database.Setup.csproj"
)

# Clean and create the staging directory where all build files will be copied
if (Test-Path $stagingDir) {
    Write-Host "Cleaning staging directory: $stagingDir"
    Remove-Item -Recurse -Force $stagingDir
}
New-Item -ItemType Directory -Path $stagingDir | Out-Null

# Publish each project. 'dotnet publish' collects all necessary files.
# We pass the version properties (-p:Version and -p:InformationalVersion)
# so the compiled assemblies have the correct version number.
foreach ($project in $projectsToPublish) {
    $projectName = (Get-Item $project).Directory.Name
    $publishDir = Join-Path $stagingDir $projectName
    Write-Host "Publishing $projectName..."
    dotnet publish $project --configuration Release --output $publishDir --self-contained false -p:Version=$appVersion -p:InformationalVersion=$fullVersionString
}
Write-Host "All projects published successfully." -ForegroundColor Green

# --- 3. Copy Additional Tools ---
# The Deployment Wizard needs the 'Tools' folder containing PowerShell scripts.
$toolsDirInStaging = Join-Path $stagingDir "Tools"
New-Item -ItemType Directory -Path $toolsDirInStaging | Out-Null
$sourceToolsDir = ".\tools" # Root-level tools folder
if (Test-Path $sourceToolsDir) {
    Write-Host "Copying deployment tools..."
    Copy-Item -Path (Join-Path $sourceToolsDir "*") -Destination $toolsDirInStaging -Recurse
} else {
    Write-Warning "Tools folder not found at '$sourceToolsDir'. The setup might be incomplete."
}

# --- 4. Compile the Setup using Inno Setup ---
if (-not (Test-Path $innoSetupCompilerPath)) {
    Write-Error "Inno Setup Compiler not found at '$innoSetupCompilerPath'."
    Write-Error "Please install Inno Setup 6 or update the path in this script."
    exit 1
}

Write-Host "Running Inno Setup Compiler to create the FULL setup..."
# We pass the full version string to the Inno Setup script so the installer is also versioned.
& $innoSetupCompilerPath /DAppVersion=$fullVersionString $fullSetupScript

if (Test-Path $cashierSetupScript) {
    Write-Host "Running Inno Setup Compiler to create the CASHIER ONLY setup..."
    & $innoSetupCompilerPath /DAppVersion=$fullVersionString $cashierSetupScript
} else {
    Write-Warning "Cashier setup script not found at '$cashierSetupScript'. Skipping."
}

Write-Host "------------------------------------------------" -ForegroundColor Cyan
Write-Host "Build and setup creation completed successfully!" -ForegroundColor Green
Write-Host "Your new setup files can be found in the 'Output' sub-folder."
