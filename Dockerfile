# =======================
# Stage 1: Build
# =======================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY *.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o /app/publish

# =======================
# Stage 2: Runtime
# =======================
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

COPY --from=build /app/publish .

# Render usa la variable PORT (normalmente 10000)
ENV ASPNETCORE_URLS=http://+:${PORT}

EXPOSE 10000

ENTRYPOINT ["dotnet", "EcommerceApp.dll"]
