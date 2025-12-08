# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies as a distinct layer for caching
COPY ["PoolMate.Api.csproj", "./"]
RUN dotnet restore "./PoolMate.Api.csproj"

# Copy remaining source code and build the application
COPY . .
RUN dotnet publish "PoolMate.Api.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Install necessary runtime dependencies if needed (e.g., for OpenTelemetry, monitoring)
# RUN apt-get update && apt-get install -y curl

EXPOSE 7127

# Copy the build output from the previous stage
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "PoolMate.Api.dll"]
