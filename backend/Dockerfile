FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the dependency layer is cached between builds
COPY Kitsuneo.slnx ./
COPY src/Kitsuneo.API/Kitsuneo.API.csproj src/Kitsuneo.API/
COPY src/Kitsuneo.Application/Kitsuneo.Application.csproj src/Kitsuneo.Application/
COPY src/Kitsuneo.Core/Kitsuneo.Core.csproj src/Kitsuneo.Core/
COPY src/Kitsuneo.Infrastructure/Kitsuneo.Infrastructure.csproj src/Kitsuneo.Infrastructure/
RUN dotnet restore src/Kitsuneo.API/Kitsuneo.API.csproj

COPY src/ src/
RUN dotnet publish src/Kitsuneo.API/Kitsuneo.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

USER $APP_UID
EXPOSE 8080

ENTRYPOINT ["dotnet", "Kitsuneo.API.dll"]
