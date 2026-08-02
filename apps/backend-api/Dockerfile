FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

RUN --mount=type=secret,id=local_ca,required=false \
    if [ -s /run/secrets/local_ca ]; then \
      cp /run/secrets/local_ca /usr/local/share/ca-certificates/rydo-local-ca.crt && \
      update-ca-certificates; \
    fi

COPY . .
RUN dotnet restore src/Rydo.Api/Rydo.Api.csproj --locked-mode
RUN dotnet publish src/Rydo.Api/Rydo.Api.csproj \
    --no-restore \
    --configuration Release \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN --mount=type=secret,id=local_ca,required=false \
    if [ -s /run/secrets/local_ca ]; then \
      cp /run/secrets/local_ca /usr/local/share/ca-certificates/rydo-local-ca.crt && \
      update-ca-certificates; \
    fi

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .
USER root
RUN mkdir -p /app/data/driver-documents && chown -R app:app /app/data
USER app
ENTRYPOINT ["dotnet", "Rydo.Api.dll"]
