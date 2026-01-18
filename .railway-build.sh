#!/bin/bash
# Railway build script that auto-detects the project location

# Find the Travel.Api.csproj file
PROJ_FILE=$(find . -name "Travel.Api.csproj" -type f | head -1)

if [ -z "$PROJ_FILE" ]; then
    echo "Error: Travel.Api.csproj not found"
    exit 1
fi

# Get the directory containing the .csproj file
PROJ_DIR=$(dirname "$PROJ_FILE")
echo "Found project at: $PROJ_DIR"

# Change to project directory and build
cd "$PROJ_DIR" || exit 1
dotnet restore
dotnet publish -c Release -o ../../out || dotnet publish -c Release -o ../out
