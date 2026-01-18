# Travel API

ASP.NET Core 8.0 Web API for travel booking system.

## Configuration

### Local Development

1. Copy `Travel.Api/appsettings.Development.json.example` to `Travel.Api/appsettings.Development.json`
2. Add your MongoDB connection string to `appsettings.Development.json`

### Production (Railway/Deployment)

Set the following environment variables:

- `MONGO_CONNECTIONSTRING` - Your MongoDB connection string
- `MONGO_DATABASENAME` - Database name (optional, defaults to "travel_db")

#### Railway Setup

1. **Root Directory**: 
   - If deploying from the `Travel` folder: Leave empty or set to `.`
   - If deploying from repository root: Set to `Travel`
2. **Build Command**: (Auto-configured via `nixpacks.toml`)
3. **Start Command**: (Auto-configured via `nixpacks.toml`)
4. **Environment Variables** (in Railway Variables tab):
   - `MONGO_CONNECTIONSTRING` = `mongodb+srv://username:password@cluster.mongodb.net/`
   - `MONGO_DATABASENAME` = `travel_db` (optional)
   - `PORT` = `8080` (or match your Railway service port)

The application will automatically use environment variables if they are set, otherwise it will fall back to `appsettings.json`.

**Note**: Railway configuration files (`railway.json` and `nixpacks.toml`) are included to help with deployment. The build commands will automatically detect the correct directory structure.

## Running Locally

```bash
cd Travel.Api
dotnet run
```

## Building for Production

```bash
cd Travel.Api
dotnet publish -c Release -o out
```
