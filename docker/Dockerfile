# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

# Copy solution and project files first (layer cache optimization)
COPY Nucleus.sln .
COPY src/Nucleus.Domain/Nucleus.Domain.csproj src/Nucleus.Domain/
COPY src/Nucleus.Application/Nucleus.Application.csproj src/Nucleus.Application/
COPY src/Nucleus.Infrastructure/Nucleus.Infrastructure.csproj src/Nucleus.Infrastructure/
COPY src/Nucleus.Api/Nucleus.Api.csproj src/Nucleus.Api/
COPY tests/Nucleus.Application.Tests/Nucleus.Application.Tests.csproj tests/Nucleus.Application.Tests/

# Restore
RUN dotnet restore

# Copy everything and build
COPY . .
RUN dotnet build --no-restore -c Release

# Run tests
RUN dotnet test --no-build -c Release --verbosity minimal

# Publish
RUN dotnet publish src/Nucleus.Api/Nucleus.Api.csproj --no-build -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Railway-specific: disable HTTPS redirect in container (Railway terminates TLS)
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "Nucleus.Api.dll"]
