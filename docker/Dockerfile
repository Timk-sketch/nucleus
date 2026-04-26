FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
# Install Node.js (required for Blazor WASM Sass/JS tooling if needed)
WORKDIR /src
COPY Nucleus.sln .
COPY src/Nucleus.Domain/Nucleus.Domain.csproj src/Nucleus.Domain/
COPY src/Nucleus.Application/Nucleus.Application.csproj src/Nucleus.Application/
COPY src/Nucleus.Infrastructure/Nucleus.Infrastructure.csproj src/Nucleus.Infrastructure/
COPY src/Nucleus.Api/Nucleus.Api.csproj src/Nucleus.Api/
COPY src/Nucleus.Web/Nucleus.Web.csproj src/Nucleus.Web/
# Restore entire solution so Blazor WASM project.assets.json is generated before publish
RUN dotnet restore Nucleus.sln
COPY . .
# Publishing the API also builds + embeds the Blazor WASM client (via ProjectReference)
RUN dotnet publish src/Nucleus.Api/Nucleus.Api.csproj -c Release -o /publish

FROM base AS final
WORKDIR /app
COPY --from=build /publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Nucleus.Api.dll"]
