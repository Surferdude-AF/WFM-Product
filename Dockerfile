# syntax=docker/dockerfile:1

# ---- build: publish the API and emit a self-contained migration bundle ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore WFM-Product.slnx
RUN dotnet publish src/Wfm.Api/Wfm.Api.csproj -c Release -o /app/publish --no-restore

# A self-contained migrator, run as the DB owner before the app starts: the
# runtime app role (wfm_app) deliberately cannot create roles or RLS policies,
# so migrations can't run from the app process. Forward-only (ADR-002).
RUN dotnet tool install --global dotnet-ef --version 10.0.9
ENV PATH="$PATH:/root/.dotnet/tools"
# Infrastructure is both the migrations project and the startup project: it owns
# the design-time DbContext factory and references EF Core Design (the API host
# deliberately does not -- it's a private dependency of the adapter layer).
RUN dotnet ef migrations bundle --self-contained -r linux-x64 \
    --project src/Modules/Forecasting/Wfm.Forecasting.Infrastructure/Wfm.Forecasting.Infrastructure.csproj \
    --startup-project src/Modules/Forecasting/Wfm.Forecasting.Infrastructure/Wfm.Forecasting.Infrastructure.csproj \
    --configuration Release \
    -o /app/efbundle

# ---- runtime: aspnet only; carries the published app and the migration bundle ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
COPY --from=build /app/efbundle ./efbundle
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Wfm.Api.dll"]
