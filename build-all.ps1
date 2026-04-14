$ErrorActionPreference = 'Stop'

function Invoke-Native {
    param(
        [scriptblock] $Command
    )

    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE."
    }
}

Write-Host 'Building StockService...'
Invoke-Native { dotnet build .\StockService\StockService.csproj }

Write-Host 'Building BillingService...'
Invoke-Native { dotnet build .\BillingService\BillingService.csproj }

Write-Host 'Building Angular frontend...'
Push-Location .\frontend
try {
    Invoke-Native { npm run build }
}
finally {
    Pop-Location
}

Write-Host 'Build completed successfully.'
