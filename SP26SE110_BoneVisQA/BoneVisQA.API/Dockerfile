# Learn about multi-stage builds: https://docs.docker.com/build/buildkit/
# Use .NET 8.0 SDK for building and ASP.NET 8.0 runtime for running

# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Copy project files first (for better layer caching - only restores when .csproj changes)
COPY BoneVisQA.API/BoneVisQA.API.csproj BoneVisQA.API/BoneVisQA.API.csproj
COPY BoneVisQA.Services/BoneVisQA.Services.csproj BoneVisQA.Services/BoneVisQA.Services.csproj
COPY BoneVisQA.Repositories/BoneVisQA.Repositories.csproj BoneVisQA.Repositories/BoneVisQA.Repositories.csproj

# Restore dependencies (cached layer unless .csproj changes)
RUN dotnet restore "BoneVisQA.API/BoneVisQA.API.csproj"

# Copy all source code
COPY BoneVisQA.API/ BoneVisQA.API/
COPY BoneVisQA.Services/ BoneVisQA.Services/
COPY BoneVisQA.Repositories/ BoneVisQA.Repositories/

# Build and publish in one step (skips intermediate build artifacts)
WORKDIR "/src/BoneVisQA.API"
RUN dotnet publish "BoneVisQA.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime (small image, only runtime needed)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

WORKDIR /app

# Install libgdiplus if needed for System.Drawing
RUN apt-get update && apt-get install -y --no-install-recommends \
    libgdiplus \
    && rm -rf /var/lib/apt/lists/*

EXPOSE 5047

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5047
ENV ASPNETCORE_ENVIRONMENT=Production

# Copy published files from build stage
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "BoneVisQA.API.dll"]
