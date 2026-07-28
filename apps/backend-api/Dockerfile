FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY . .
RUN dotnet restore src/Rydo.Api/Rydo.Api.csproj --locked-mode
RUN dotnet publish src/Rydo.Api/Rydo.Api.csproj \
    --no-restore \
    --configuration Release \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "Rydo.Api.dll"]
