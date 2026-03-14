# ========================================
# EchoForge API — Multi-stage Dockerfile
# ========================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# Install FFmpeg for video composition
RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg && \
    rm -rf /var/lib/apt/lists/*

# ---- Build Stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore (layer caching)
COPY src/EchoForge.Core/EchoForge.Core.csproj src/EchoForge.Core/
COPY src/EchoForge.Infrastructure/EchoForge.Infrastructure.csproj src/EchoForge.Infrastructure/
COPY src/EchoForge.API/EchoForge.API.csproj src/EchoForge.API/
RUN dotnet restore src/EchoForge.API/EchoForge.API.csproj

# Copy everything and build
COPY src/ src/
RUN dotnet publish src/EchoForge.API/EchoForge.API.csproj -c Release -o /app/publish --no-restore

# ---- Runtime Stage ----
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# Create updates directory for auto-update system
RUN mkdir -p /app/wwwroot/updates

# Environment variables (overridable in Coolify)
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ConnectionStrings__DefaultConnection=""

ENTRYPOINT ["dotnet", "EchoForge.API.dll"]
