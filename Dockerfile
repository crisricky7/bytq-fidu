# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restaurar primero solo con los .csproj aprovecha la caché de capas.
COPY src/Auditoria.Domain/Auditoria.Domain.csproj src/Auditoria.Domain/
COPY src/Auditoria.Application/Auditoria.Application.csproj src/Auditoria.Application/
COPY src/Auditoria.Infrastructure/Auditoria.Infrastructure.csproj src/Auditoria.Infrastructure/
COPY src/Auditoria.Api/Auditoria.Api.csproj src/Auditoria.Api/
RUN dotnet restore src/Auditoria.Api/Auditoria.Api.csproj

COPY src/ src/
RUN dotnet publish src/Auditoria.Api/Auditoria.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_gcServer=0
COPY --from=build /app/publish .
# Usuario sin privilegios que trae la imagen base.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Auditoria.Api.dll"]
