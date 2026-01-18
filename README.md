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

1. Go to your Railway project settings
2. Navigate to "Variables" tab
3. Add the following environment variables:
   - `MONGO_CONNECTIONSTRING` = `mongodb+srv://username:password@cluster.mongodb.net/`
   - `MONGO_DATABASENAME` = `travel_db` (optional)

The application will automatically use environment variables if they are set, otherwise it will fall back to `appsettings.json`.

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
