# Multi-stage Dockerfile for ESS Leave System
# Optimized for Railway, Render, and local Docker

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy everything and publish
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage - minimal image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copy published files from build stage
COPY --from=build /app/publish .

# Environment variables (can be overridden at runtime)
# Note: Don't hard-code ASPNETCORE_ENVIRONMENT to allow dev/prod flexibility
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Health check (optional but recommended)
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "ESSLeaveSystem.dll"]