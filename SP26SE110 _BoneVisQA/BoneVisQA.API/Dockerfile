# Learn about multi-stage builds: https://docs.docker.com/build/buildkit/
# Use .NET 8.0 SDK for building and ASP.NET 8.0 runtime for running

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Copy project files from parent directory
COPY ../BoneVisQA.API/BoneVisQA.API.csproj BoneVisQA.API/BoneVisQA.API.csproj
COPY ../BoneVisQA.Services/BoneVisQA.Services.csproj BoneVisQA.Services/BoneVisQA.Services.csproj
COPY ../BoneVisQA.Repositories/BoneVisQA.Repositories.csproj BoneVisQA.Repositories/BoneVisQA.Repositories.csproj

# Restore dependencies
RUN dotnet restore "BoneVisQA.API/BoneVisQA.API.csproj"

# Copy all source files from parent
COPY ../ .

# Build the project
WORKDIR "/src/BoneVisQA.API"
RUN dotnet build "BoneVisQA.API.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish

RUN dotnet publish "BoneVisQA.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

WORKDIR /app

# Install libgdiplus if needed for System.Drawing
RUN apt-get update && apt-get install -y --no-install-recommends \
    libgdiplus \
    && rm -rf /var/lib/apt/lists/*

EXPOSE 5046

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5046
ENV ASPNETCORE_ENVIRONMENT=Production

# Copy published files from publish stage
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "BoneVisQA.API.dll"]
