# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project and restore dependencies
COPY . .
RUN dotnet restore ./backend.csproj

# Publish the app
RUN dotnet publish ./backend.csproj -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .

# Railway provides PORT at runtime; use shell form so ${PORT} expands at container start
EXPOSE 8080

ENTRYPOINT dotnet backend.dll --urls "http://0.0.0.0:${PORT:-8080}"
