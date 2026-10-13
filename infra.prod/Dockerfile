# Build context: repo root (docker build -f infra.prod/Dockerfile .)
# On Railway: RAILWAY_DOCKERFILE_PATH=infra.prod/Dockerfile

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY RealTime.csproj ./
RUN dotnet restore RealTime.csproj

COPY . .
RUN rm -rf bin obj \
    && dotnet publish RealTime.csproj -c Release -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out ./

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 4988
USER app

# Kestrel:Endpoints in appsettings takes precedence over ASPNETCORE_URLS,
# so the endpoint is overridden with the PORT injected by Railway.
CMD ["sh", "-c", "export Kestrel__Endpoints__Http__Url=\"http://0.0.0.0:${PORT:-4988}\"; exec dotnet RealTime.dll"]
