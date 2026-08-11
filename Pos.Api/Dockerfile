# ============================================================
# POS API — Multi-stage Dockerfile (build → test → publish → runtime)
#
# PLACE: POS/Project/Backend/Pos/Pos.Api/Dockerfile
# (part of your local structure: Backend/Pos/Pos.Api/)
#
# The Dockerfile lives INSIDE Pos.Api/ but the build context is
# the solution root (docker-compose sets context: ./Backend/Pos),
# so COPY paths are relative to Backend/Pos/, not this file.
# ============================================================

# ---------- STAGE 1: RESTORE + BUILD ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first → layer caching (only re-restore when csproj changes)
COPY Pos.Api/Pos.Api.csproj Pos.Api/
COPY Pos.Test/Pos.Test.csproj Pos.Test/
RUN dotnet restore Pos.Api/Pos.Api.csproj
RUN dotnet restore Pos.Test/Pos.Test.csproj

# Copy all source and build Release
COPY . .
RUN dotnet publish Pos.Api/Pos.Api.csproj -c Release -o /app

# ---------- STAGE 2: TEST (uses the build stage artifacts) ----------
FROM build AS test
# Run all tests — FAILING tests fail the whole docker build
RUN dotnet test Pos.Test/Pos.Test.csproj -c Release --no-build --logger "console;verbosity=minimal"

# ---------- STAGE 3: RUNTIME (slim) ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# Install curl — the aspnet image does NOT ship curl or wget!
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
USER app
WORKDIR /app

# Non-root user (created by the aspnet base image)
USER app

# Health check — compose waits on this before routing traffic
HEALTHCHECK --interval=15s --timeout=3s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:8080/api/health || exit 1

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app .

ENTRYPOINT ["dotnet", "Pos.Api.dll"]
