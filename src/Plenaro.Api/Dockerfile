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
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Plenaro.Api.dll"]
