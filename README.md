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

2. **IMPORTANT - Remove Custom Build Command**:
   - Go to Railway Dashboard → Your Service → Settings → "Build & Deploy"
   - **DELETE or CLEAR** any "Custom Build Command" 
   - **DELETE or CLEAR** any "Custom Start Command"
   - Let Railway use the `nixpacks.toml` configuration automatically

3. **If you must use Custom Build Command**, use this:
   ```bash
   find . -name "Travel.Api.csproj" -type f | head -1 | xargs dirname | xargs -I {} sh -c 'cd {} && dotnet restore && dotnet publish -c Release -o ../../out'
   ```

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
