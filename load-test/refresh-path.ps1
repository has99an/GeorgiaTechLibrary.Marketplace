# Refresh PATH environment variable to include newly installed programs
$env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")

Write-Host "PATH refreshed. K6 should now be available." -ForegroundColor Green
Write-Host "Verify with: k6 version" -ForegroundColor Yellow


