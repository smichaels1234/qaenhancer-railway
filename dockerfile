# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend.csproj ./
RUN dotnet restore ./backend.csproj

COPY . .
RUN dotnet publish ./backend.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
# Shell form so ${PORT} expands at container start
ENTRYPOINT dotnet backend.dll --urls "http://0.0.0.0:${PORT:-8080}"
