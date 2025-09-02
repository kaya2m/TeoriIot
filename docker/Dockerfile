# Use standard .NET 8 images
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first for better layer caching
COPY ["src/IotIngest.Api/IotIngest.Api.csproj", "src/IotIngest.Api/"]
COPY ["tests/IotIngest.Tests/IotIngest.Tests.csproj", "tests/IotIngest.Tests/"]
COPY ["IotIngest.sln", "./"]

# Restore packages - this layer will be cached if project files don't change
RUN dotnet restore "IotIngest.sln" --verbosity normal

# Copy source code - this layer changes more frequently
COPY . .

# Build and publish in one step
RUN dotnet publish "src/IotIngest.Api/IotIngest.Api.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "IotIngest.Api.dll"]