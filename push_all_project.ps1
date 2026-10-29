# PowerShell script to force‑push the entire project directory to GitHub in a single step
# Run this script from the project root (it will change to its own directory).

# Change to the directory where this script resides
Set-Location -Path "$(Split-Path -Parent $MyInvocation.MyCommand.Path)"

# Initialise a Git repository if it does not exist
if (-not (Test-Path -Path ".git")) {
    git init
}

# Remote URL (replace if different)
$remoteUrl = "https://github.com/josephao64/sistemi-copia-funcional.git"

# Add remote "origin" if it is not already configured
if (-not (git remote | Select-String "origin")) {
    git remote add origin $remoteUrl
}

# Stage all files (including deletions)
git add -A

# Commit if there are any staged changes
if ((git status --porcelain) -ne "") {
    $msg = "auto commit: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")"
    git commit -m $msg
}

# Ensure we are on the main branch
git branch -M main

# Force‑push to remote, setting upstream
git push -u origin main --force

Write-Host "Project successfully force‑pushed to GitHub."

