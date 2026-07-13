# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS source
WORKDIR /src
COPY . .

FROM source AS publish
RUN --mount=type=cache,id=bunkfy-nuget,target=/root/.nuget/packages \
    dotnet publish src/BunkFy.Host.Api/BunkFy.Host.Api.csproj -c Release -o /out/api --nologo \
    && dotnet publish src/BunkFy.Host.Worker/BunkFy.Host.Worker.csproj -c Release -o /out/worker --nologo \
    && dotnet publish src/BunkFy.Host.AdminApi/BunkFy.Host.AdminApi.csproj -c Release -o /out/admin-api --nologo \
    && dotnet publish src/BunkFy.Host.AdminCli/BunkFy.Host.AdminCli.csproj -c Release -o /out/admin-cli --nologo \
    && dotnet publish src/BunkFy.Host.Migrations/BunkFy.Host.Migrations.csproj -c Release -o /out/migrations --nologo

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime-base
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /var/lib/bunkfy/data-protection /var/lib/bunkfy/file-drop \
    && chown -R app:app /var/lib/bunkfy
USER app
WORKDIR /app

FROM runtime-base AS backend
COPY --from=publish --chown=app:app /out /opt/bunkfy
WORKDIR /opt/bunkfy/api
EXPOSE 8080
ENTRYPOINT ["dotnet"]
CMD ["BunkFy.Host.Api.dll"]
