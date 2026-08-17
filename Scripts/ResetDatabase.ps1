# PowerShell script to reset the database for Identity
# WARNING: This will delete all user data!

Write-Host "Stopping backend if running..." -ForegroundColor Yellow

# Navigate to backend directory
Set-Location "C:\Users\smich\Documents\vscode\QAEnhancer\backend"

# Drop and recreate the database (this is the cleanest approach)
Write-Host "Dropping existing database..." -ForegroundColor Yellow
dotnet ef database drop --force

Write-Host "Recreating database with all migrations..." -ForegroundColor Green
dotnet ef database update

Write-Host "Database reset complete!" -ForegroundColor Green
Write-Host "You can now register new users without conflicts." -ForegroundColor Cyan
