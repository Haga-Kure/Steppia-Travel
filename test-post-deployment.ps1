# Post-Deployment API Test Script
# Run this after Railway deployment completes to verify all endpoints work

$baseUrl = "https://steppia-travel-production.up.railway.app"
$global:token = $null
$global:testResults = @()

function Test-Endpoint {
    param(
        [string]$Name,
        [string]$Method,
        [string]$Url,
        [hashtable]$Headers = @{},
        [string]$Body = $null,
        [int]$ExpectedStatus = 200
    )
    
    Write-Host "`nTesting: $Name" -ForegroundColor Yellow
    Write-Host "  $Method $Url" -ForegroundColor Gray
    
    try {
        $params = @{
            Uri = $Url
            Method = $Method
            Headers = $Headers
            ErrorAction = "Stop"
        }
        
        if ($Body) {
            $params.Body = $Body
            $params.ContentType = "application/json"
        }
        
        $response = Invoke-RestMethod @params
        $statusCode = 200
        
        Write-Host "  ✓ Success (Status: $statusCode)" -ForegroundColor Green
        $global:testResults += @{
            Name = $Name
            Status = "PASS"
            StatusCode = $statusCode
        }
        return $response
    } catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        if (-not $statusCode) { $statusCode = "Error" }
        
        Write-Host "  ✗ Failed (Status: $statusCode)" -ForegroundColor Red
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
        
        $global:testResults += @{
            Name = $Name
            Status = "FAIL"
            StatusCode = $statusCode
            Error = $_.Exception.Message
        }
        return $null
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Post-Deployment API Test Suite" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan
Write-Host "Base URL: $baseUrl`n" -ForegroundColor Gray

# Test 1: Health Check
Test-Endpoint -Name "Health Check" -Method "GET" -Url "$baseUrl/health/mongo"

# Test 2: Tours List with Pagination
Test-Endpoint -Name "Tours List (Pagination)" -Method "GET" -Url "${baseUrl}/tours?page=1`&pageSize=2"

# Test 3: Tours Search
Test-Endpoint -Name "Tours Search" -Method "GET" -Url "${baseUrl}/tours?search=mongolia`&pageSize=5"

# Test 4: Admin Login
$loginBody = @{
    username = "admin"
    password = "admin123"
} | ConvertTo-Json

$loginResult = Test-Endpoint -Name "Admin Login" -Method "POST" -Url "$baseUrl/admin/auth/login" -Body $loginBody -ExpectedStatus 200

if ($loginResult -and $loginResult.token) {
    $global:token = $loginResult.token
    Write-Host "  Token received (length: $($loginResult.token.Length))" -ForegroundColor Green
    
    # Test 5: Admin Auth Me
    $headers = @{ Authorization = "Bearer $global:token" }
    Test-Endpoint -Name "Admin Auth Me" -Method "GET" -Url "$baseUrl/admin/auth/me" -Headers $headers
    
    # Test 6: Admin Tours
    Test-Endpoint -Name "Admin Tours (Pagination)" -Method "GET" -Url "${baseUrl}/admin/tours?page=1`&pageSize=2" -Headers $headers
    
    # Test 7: Admin Bookings
    Test-Endpoint -Name "Admin Bookings (Pagination)" -Method "GET" -Url "${baseUrl}/admin/bookings?page=1`&pageSize=5" -Headers $headers
    
    # Test 8: Admin Bookings Stats
    Test-Endpoint -Name "Admin Bookings Stats" -Method "GET" -Url "$baseUrl/admin/bookings/stats" -Headers $headers
} else {
    Write-Host "`n⚠ Skipping admin endpoints (login failed)" -ForegroundColor Yellow
    Write-Host "  Check if JWT_SECRET is set in Railway environment variables" -ForegroundColor Yellow
}

# Test 9: Tour Dates (Public)
$slug = "khovd-overland-multi-ethnic-altai-expedition-7d"
$datesResult = Test-Endpoint -Name "Tour Dates (Public)" -Method "GET" -Url "$baseUrl/tours/$slug/dates" -ExpectedStatus 200

if ($datesResult) {
    Write-Host "  Found: $($datesResult.Count) available dates" -ForegroundColor Green
} else {
    Write-Host "  Note: This may return empty array if tour has no dates" -ForegroundColor Gray
}

# Test 10: Create a test booking to test payment status
Write-Host "`nCreating test booking for payment status test..." -ForegroundColor Yellow
$bookingBody = @{
    tourId = "69693d293953f333a63ad670"
    tourType = "Private"
    travelDate = "2024-12-25"
    contact = @{
        fullName = "Test User"
        email = "test@example.com"
        phone = "+1234567890"
        country = "USA"
    }
    guests = @(
        @{
            fullName = "Test User"
            age = 30
            passportNo = "TEST123"
        }
    )
} | ConvertTo-Json -Depth 10

$bookingResult = Test-Endpoint -Name "Create Booking" -Method "POST" -Url "$baseUrl/bookings" -Body $bookingBody -ExpectedStatus 200

if ($bookingResult -and $bookingResult.bookingCode) {
    $bookingCode = $bookingResult.bookingCode
    Write-Host "  Booking created: $bookingCode" -ForegroundColor Green
    
    # Test 11: Get Booking by Code
    Test-Endpoint -Name "Get Booking by Code" -Method "GET" -Url "$baseUrl/bookings/$bookingCode"
    
    # Test 12: Create Payment
    $paymentBody = @{
        bookingCode = $bookingCode
        provider = "stripe"
    } | ConvertTo-Json
    
    $paymentResult = Test-Endpoint -Name "Create Payment" -Method "POST" -Url "$baseUrl/payments" -Body $paymentBody -ExpectedStatus 200
    
    if ($paymentResult -and $paymentResult.invoiceId) {
        $invoiceId = $paymentResult.invoiceId
        Write-Host "  Payment created: $invoiceId" -ForegroundColor Green
        
        # Test 13: Get Payment Status by Invoice ID
        Test-Endpoint -Name "Get Payment by Invoice ID" -Method "GET" -Url "$baseUrl/payments/$invoiceId"
        
        # Test 14: Get Payment Status for Booking
        Test-Endpoint -Name "Get Payment Status for Booking" -Method "GET" -Url "$baseUrl/bookings/$bookingCode/payment"
    }
}

# Summary
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Test Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$passed = ($global:testResults | Where-Object { $_.Status -eq "PASS" }).Count
$failed = ($global:testResults | Where-Object { $_.Status -eq "FAIL" }).Count
$total = $global:testResults.Count

Write-Host "`nTotal Tests: $total" -ForegroundColor White
Write-Host "Passed: $passed" -ForegroundColor Green
Write-Host "Failed: $failed" -ForegroundColor $(if ($failed -gt 0) { "Red" } else { "Green" })

if ($failed -gt 0) {
    Write-Host "`nFailed Tests:" -ForegroundColor Red
    $global:testResults | Where-Object { $_.Status -eq "FAIL" } | ForEach-Object {
        Write-Host "  - $($_.Name): $($_.StatusCode) - $($_.Error)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
