# PowerShell test script for Travel API
# Replace YOUR_RAILWAY_URL with your actual Railway URL
$RailwayUrl = "https://steppia-travel-production.up.railway.app"

Write-Host "🚀 Testing Travel API at $RailwayUrl" -ForegroundColor Cyan
Write-Host ""

Write-Host "1️⃣ Testing MongoDB Health Check..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$RailwayUrl/health/mongo" -Method Get
    $response | ConvertTo-Json -Depth 10
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}
Write-Host ""

Write-Host "2️⃣ Getting Tours List..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$RailwayUrl/tours" -Method Get
    $response | ConvertTo-Json -Depth 10
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}
Write-Host ""

Write-Host "3️⃣ Testing Swagger UI..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "$RailwayUrl/swagger" -Method Get
    Write-Host "Status: $($response.StatusCode)" -ForegroundColor Green
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}
Write-Host ""

Write-Host "✅ Testing complete!" -ForegroundColor Green
