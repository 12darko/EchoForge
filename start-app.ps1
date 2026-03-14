Write-Host "Starting EchoForge API (Backend)..." -ForegroundColor Cyan
Start-Process dotnet "run --project src/EchoForge.API/EchoForge.API.csproj" -WindowStyle Minimized

Write-Host "Waiting for API to initialize..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

Write-Host "Starting EchoForge Board (Frontend)..." -ForegroundColor Cyan
dotnet run --project src/EchoForge.WPF/EchoForge.WPF.csproj
