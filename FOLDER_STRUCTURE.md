# Travel Folder Structure

```
Travel/
├── .gitignore                          # Git ignore rules
├── .railway-build.sh                   # Railway build script (optional)
├── Procfile                            # Railway Procfile for start command
├── NuGet.Config                        # NuGet configuration (solution level)
├── nixpacks.toml                      # Railway/Nixpacks build configuration
├── railway.json                        # Railway deployment configuration
├── README.md                           # Project documentation
├── Travel.sln                         # Visual Studio solution file
│
├── Travel.Api/                        # Main API project
│   ├── Travel.Api.csproj              # Project file
│   ├── Program.cs                     # Main application entry point
│   ├── appsettings.json               # Production settings (safe to commit)
│   ├── appsettings.Development.json   # Local dev settings (gitignored)
│   ├── appsettings.Development.json.example  # Template for dev settings
│   │
│   ├── Dtos/                          # Data Transfer Objects
│   │   ├── CreateBookingRequest.cs
│   │   ├── CreateBookingResponse.cs
│   │   ├── CreatePaymentRequest.cs
│   │   ├── PaymentWebhookRequest.cs
│   │   └── TourDto.cs
│   │
│   ├── Models/                         # Domain models
│   │   ├── Booking.cs
│   │   ├── Payment.cs
│   │   ├── Tour.cs
│   │   └── TourDate.cs
│   │
│   └── Properties/
│       └── launchSettings.json        # Launch configuration
│
├── out/                               # Build output (gitignored, created during build)
│   └── (compiled DLLs and dependencies)
│
└── test-api.*                         # Test scripts (optional)
    ├── test-api.http
    ├── test-api.ps1
    └── test-api.sh
```

## Key Files for Railway:

1. **`nixpacks.toml`** - Tells Railway how to build your app
2. **`Procfile`** - Alternative start command (optional)
3. **`Travel.Api/Travel.Api.csproj`** - The .NET project file Railway needs to find
4. **`Travel.Api/Program.cs`** - Your application code

## Railway Build Process:

1. Railway finds `Travel.Api.csproj` (auto-detected)
2. Runs `dotnet restore` in that directory
3. Runs `dotnet publish -c Release -o ../out`
4. Starts with `dotnet out/Travel.Api.dll`

## Important Notes:

- `out/` folder is gitignored (build artifacts)
- `appsettings.Development.json` is gitignored (contains secrets)
- `bin/` and `obj/` folders are gitignored (build artifacts)
- Railway will create the `out/` folder during build
