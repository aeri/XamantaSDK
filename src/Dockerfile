FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:6a94333d37514e385650a3c81a55e5350b67253dbe136e9cf17e499c35606a8c AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:2fa828c68761b1b8c23d7662dc134421b9d3b59fe1425fdbc80804e390cdb24d AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/XamantaSDK.csproj", "XamantaSDK/"]
RUN dotnet restore "./XamantaSDK/XamantaSDK.csproj"
COPY src/ XamantaSDK/
WORKDIR "/src/XamantaSDK"
RUN dotnet build "./XamantaSDK.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./XamantaSDK.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /data && chown $APP_UID:$APP_UID /data
USER $APP_UID
HEALTHCHECK --interval=10s --timeout=3s --start-period=30s --retries=5 \
    CMD curl -fsS --http2-prior-knowledge http://127.0.0.1:5000/health || exit 1
ENTRYPOINT ["dotnet", "XamantaSDK.dll"]