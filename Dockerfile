# One image serves both halves: the Angular build lands in the API's wwwroot, so the
# client and /api share an origin and no CORS or API URL configuration is needed.

# ---- Angular build ----
FROM node:24-alpine AS client
WORKDIR /client
COPY client/package.json client/package-lock.json ./
RUN npm ci
COPY client/ ./
RUN npm run build -- --configuration production

# ---- API build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
# Restore first, against project files only, so a source-only edit reuses this layer.
COPY server/PMGIS.Domain/PMGIS.Domain.csproj          server/PMGIS.Domain/
COPY server/PMGIS.Infrastructure/PMGIS.Infrastructure.csproj server/PMGIS.Infrastructure/
COPY server/PMGIS.ServiceDefaults/PMGIS.ServiceDefaults.csproj server/PMGIS.ServiceDefaults/
COPY server/PMGIS.Api/PMGIS.Api.csproj                server/PMGIS.Api/
RUN dotnet restore server/PMGIS.Api/PMGIS.Api.csproj
COPY server/ server/
RUN dotnet publish server/PMGIS.Api/PMGIS.Api.csproj -c Release -o /app --no-restore

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=api /app ./
COPY --from=client /client/dist/client/browser ./wwwroot
# Fly routes to this port; ASP.NET Core reads it from ASPNETCORE_HTTP_PORTS.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "PMGIS.Api.dll"]
