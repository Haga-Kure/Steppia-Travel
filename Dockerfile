# Use Railway's .NET 8 base image
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy solution and project files
COPY Travel.sln .
COPY Travel.Api/*.csproj ./Travel.Api/
COPY NuGet.Config .

# Restore dependencies
RUN dotnet restore Travel.sln

# Copy everything else and build
COPY Travel.Api/ ./Travel.Api/
RUN dotnet publish Travel.Api/Travel.Api.csproj -c Release -o /app/out -r linux-x64 --self-contained true

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/out .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENTRYPOINT ["./Travel.Api"]
