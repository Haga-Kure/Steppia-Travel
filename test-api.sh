#!/bin/bash

# Test script for Travel API
# Replace YOUR_RAILWAY_URL with your actual Railway URL
RAILWAY_URL="YOUR_RAILWAY_URL"

echo "🚀 Testing Travel API at $RAILWAY_URL"
echo ""

echo "1️⃣ Testing MongoDB Health Check..."
curl -s "$RAILWAY_URL/health/mongo" | jq .
echo ""

echo "2️⃣ Getting Tours List..."
curl -s "$RAILWAY_URL/tours" | jq .
echo ""

echo "3️⃣ Testing Swagger UI (should return HTML)..."
curl -s -o /dev/null -w "Status: %{http_code}\n" "$RAILWAY_URL/swagger"
echo ""

echo "✅ Testing complete!"
