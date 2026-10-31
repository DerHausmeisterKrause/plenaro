FROM node:22-alpine AS frontend
WORKDIR /frontend
COPY frontend/package.json ./
RUN npm install --no-audit --no-fund
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Plenaro.Api/Plenaro.Api.csproj src/Plenaro.Api/
RUN dotnet restore src/Plenaro.Api/Plenaro.Api.csproj
COPY src/Plenaro.Api/ src/Plenaro.Api/
RUN dotnet publish src/Plenaro.Api/Plenaro.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
# Npgsql requires GSSAPI native runtime library during CLI/setup-token as well
# as normal ASP.NET startup. Keep Debian runtime dependencies explicit.
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
# React/Vite-Ausgabe ersetzt die Phase-1-Statusseite.
COPY --from=frontend /frontend/dist/ /app/wwwroot/
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Plenaro.Api.dll"]
