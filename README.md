# Travel API

ASP.NET Core 8.0 Web API for travel booking system.

## Configuration

### Local Development

1. Copy `Travel.Api/appsettings.Development.json.example` to `Travel.Api/appsettings.Development.json`
2. Add your MongoDB connection string to `appsettings.Development.json`

### Production (Railway/Deployment)

#### Required Environment Variables

- `MONGO_CONNECTIONSTRING` - Your MongoDB connection string
- `JWT_SECRET` - JWT secret key for token signing (minimum 32 characters)

#### Optional Environment Variables

- `MONGO_DATABASENAME` - Database name (default: "travel_db")
- `JWT_ISSUER` - JWT issuer (default: "steppia-travel-api")
- `JWT_AUDIENCE` - JWT audience (default: "steppia-travel-admin")
- `ADMIN_USERNAME` - Initial admin username (default: "admin")
- `ADMIN_PASSWORD` - Initial admin password (default: "admin123")
- `PORT` - Server port (Railway sets this automatically)

#### Railway Setup

1. **Root Directory**: 
   - If deploying from the `Travel` folder: Leave empty or set to `.`
   - If deploying from repository root: Set to `Travel`

2. **IMPORTANT - Railway Build Configuration**:
   - Go to Railway Dashboard → Your Service → Settings → "Build & Deploy"
   - **Option A (Recommended)**: DELETE or CLEAR any "Custom Build Command" and "Custom Start Command"
     - Let Railway auto-detect and use `nixpacks.toml` or `Dockerfile`
   - **Option B**: If Railway isn't detecting nixpacks.toml, set Builder to "Dockerfile" 
     - Railway will use the `Dockerfile` for building

3. **If you must use Custom Build Command**, use this:
   ```bash
   find . -name "Travel.Api.csproj" -type f | head -1 | xargs dirname | xargs -I {} sh -c 'cd {} && dotnet restore && dotnet publish -c Release -o ../../out'
   ```

4. **Environment Variables** (in Railway Variables tab):
   - `MONGO_CONNECTIONSTRING` = `mongodb+srv://username:password@cluster.mongodb.net/` (Required)
   - `JWT_SECRET` = `your-secret-key-minimum-32-characters-long` (Required)
   - `MONGO_DATABASENAME` = `travel_db` (Optional)
   - `JWT_ISSUER` = `steppia-travel-api` (Optional)
   - `JWT_AUDIENCE` = `steppia-travel-admin` (Optional)
   - `ADMIN_USERNAME` = `admin` (Optional, for initial admin creation)
   - `ADMIN_PASSWORD` = `your-secure-password` (Optional, for initial admin creation)
   - `PORT` = `8080` (Optional, Railway sets this automatically)

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
