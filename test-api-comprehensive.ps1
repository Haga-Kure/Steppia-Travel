# Comprehensive API Test Script
$baseUrl = "https://steppia-travel-production.up.railway.app"
$global:token = $null

Write-Host "`n=== Testing Travel API ===" -ForegroundColor Cyan
Write-Host "Base URL: $baseUrl`n" -ForegroundColor Gray

# Test 1: Health Check
Write-Host "1. Health Check..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "$baseUrl/health/mongo" -Method Get
    Write-Host "   ✓ MongoDB Health: $($health.ok)" -ForegroundColor Green
} catch {
    Write-Host "   ✗ Failed: $_" -ForegroundColor Red
}

# Test 2: Tours List with Pagination
Write-Host "`n2. Tours List (Pagination)..." -ForegroundColor Yellow
try {
    $url = "${baseUrl}/tours?page=1`&pageSize=2"
    $tours = Invoke-RestMethod -Uri $url -Method Get
    Write-Host "   ✓ Returned: $($tours.data.Count) tours" -ForegroundColor Green
    Write-Host "   ✓ Page: $($tours.page)/$($tours.totalPages), Total: $($tours.total)" -ForegroundColor Green
} catch {
    Write-Host "   ✗ Failed: $_" -ForegroundColor Red
}

# Test 3: Tours Search and Filter
Write-Host "`n3. Tours Search and Filter..." -ForegroundColor Yellow
try {
    $url = "${baseUrl}/tours?search=mongolia`&type=Group`&minPrice=2000`&maxPrice=3000"
    $tours = Invoke-RestMethod -Uri $url -Method Get
    Write-Host "   ✓ Found: $($tours.data.Count) tours matching criteria" -ForegroundColor Green
} catch {
    Write-Host "   ✗ Failed: $_" -ForegroundColor Red
}

# Test 4: Admin Login
Write-Host "`n4. Admin Login..." -ForegroundColor Yellow
try {
    $loginBody = @{
        username = "admin"
        password = "admin123"
    } | ConvertTo-Json
    
    $login = Invoke-RestMethod -Uri "$baseUrl/admin/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
    $global:token = $login.token
    Write-Host "   ✓ Login successful! Token received (length: $($login.token.Length))" -ForegroundColor Green
} catch {
    Write-Host "   ✗ Failed: $_" -ForegroundColor Red
    $global:token = $null
}

# Test 5: Admin Tours with Pagination
if ($global:token) {
    Write-Host "`n5. Admin Tours (Pagination)..." -ForegroundColor Yellow
    try {
        $headers = @{
            Authorization = "Bearer $global:token"
        }
        $url = "${baseUrl}/admin/tours?page=1`&pageSize=3"
        $adminTours = Invoke-RestMethod -Uri $url -Method Get -Headers $headers
        Write-Host "   ✓ Returned: $($adminTours.data.Count) tours" -ForegroundColor Green
        Write-Host "   ✓ Page: $($adminTours.page)/$($adminTours.totalPages), Total: $($adminTours.total)" -ForegroundColor Green
    } catch {
        Write-Host "   ✗ Failed: $_" -ForegroundColor Red
    }
} else {
    Write-Host "`n5. Skipped (no token)" -ForegroundColor Gray
}

# Test 6: Admin Bookings with Pagination
if ($global:token) {
    Write-Host "`n6. Admin Bookings (Pagination)..." -ForegroundColor Yellow
    try {
        $headers = @{
            Authorization = "Bearer $global:token"
        }
        $url = "${baseUrl}/admin/bookings?page=1`&pageSize=5"
        $bookings = Invoke-RestMethod -Uri $url -Method Get -Headers $headers
        Write-Host "   ✓ Returned: $($bookings.data.Count) bookings" -ForegroundColor Green
        Write-Host "   ✓ Page: $($bookings.page)/$($bookings.totalPages), Total: $($bookings.total)" -ForegroundColor Green
    } catch {
        Write-Host "   ✗ Failed: $_" -ForegroundColor Red
    }
} else {
    Write-Host "`n6. Skipped (no token)" -ForegroundColor Gray
}

# Test 7: Tour Dates (Public)
Write-Host "`n7. Tour Dates (Public)..." -ForegroundColor Yellow
try {
    $slug = "khovd-overland-multi-ethnic-altai-expedition-7d"
    $dates = Invoke-RestMethod -Uri "$baseUrl/tours/$slug/dates" -Method Get
    Write-Host "   ✓ Found: $($dates.Count) available dates" -ForegroundColor Green
} catch {
    Write-Host "   ✗ Failed: $_" -ForegroundColor Red
}

# Test 8: Admin Password Change
if ($global:token) {
    Write-Host "`n8. Admin Password Change..." -ForegroundColor Yellow
    try {
        $headers = @{
            Authorization = "Bearer $global:token"
        }
        $changePwdBody = @{
            currentPassword = "admin123"
            newPassword = "newpassword123"
        } | ConvertTo-Json
        
        $result = Invoke-RestMethod -Uri "$baseUrl/admin/auth/change-password" -Method Put -Body $changePwdBody -ContentType "application/json" -Headers $headers
        Write-Host "   ✓ Password changed successfully" -ForegroundColor Green
        
        # Change it back
        $changeBackBody = @{
            currentPassword = "newpassword123"
            newPassword = "admin123"
        } | ConvertTo-Json
        Invoke-RestMethod -Uri "$baseUrl/admin/auth/change-password" -Method Put -Body $changeBackBody -ContentType "application/json" -Headers $headers | Out-Null
        Write-Host "   ✓ Password changed back to original" -ForegroundColor Green
    } catch {
        Write-Host "   ✗ Failed: $_" -ForegroundColor Red
    }
} else {
    Write-Host "`n8. Skipped (no token)" -ForegroundColor Gray
}

Write-Host "`n=== Test Complete ===" -ForegroundColor Cyan
