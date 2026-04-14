$ErrorActionPreference = 'Stop'

Write-Host 'Building StockService...'
dotnet build .\StockService\StockService.csproj

Write-Host 'Building BillingService...'
dotnet build .\BillingService\BillingService.csproj

Write-Host 'Building Angular frontend...'
Push-Location .\frontend
npm.cmd run build
Pop-Location

Write-Host 'Build completed successfully.'
