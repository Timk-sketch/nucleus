FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY Nucleus.sln .
COPY src/Nucleus.Domain/Nucleus.Domain.csproj src/Nucleus.Domain/
COPY src/Nucleus.Application/Nucleus.Application.csproj src/Nucleus.Application/
COPY src/Nucleus.Infrastructure/Nucleus.Infrastructure.csproj src/Nucleus.Infrastructure/
COPY src/Nucleus.Api/Nucleus.Api.csproj src/Nucleus.Api/
RUN dotnet restore src/Nucleus.Api/Nucleus.Api.csproj
COPY . .
RUN dotnet publish src/Nucleus.Api/Nucleus.Api.csproj -c Release -o /publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Nucleus.Api.dll"]
