# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files first (better Docker layer caching)
COPY ["src/LifeLinkLanka.Domain/LifeLinkLanka.Domain.csproj", "src/LifeLinkLanka.Domain/"]
COPY ["src/LifeLinkLanka.Application/LifeLinkLanka.Application.csproj", "src/LifeLinkLanka.Application/"]
COPY ["src/LifeLinkLanka.Infrastructure/LifeLinkLanka.Infrastructure.csproj", "src/LifeLinkLanka.Infrastructure/"]
COPY ["src/LifeLinkLanka.API/LifeLinkLanka.API.csproj", "src/LifeLinkLanka.API/"]

RUN dotnet restore "src/LifeLinkLanka.API/LifeLinkLanka.API.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/src/LifeLinkLanka.API"
RUN dotnet publish "LifeLinkLanka.API.csproj" -c Release -o /app/publish

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Render injects a PORT environment variable — Kestrel must bind to it
ENV ASPNETCORE_URLS=http://+:${PORT:-8080}
EXPOSE 8080

ENTRYPOINT ["dotnet", "LifeLinkLanka.API.dll"]