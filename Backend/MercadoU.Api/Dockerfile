# ETAPA DE COMPILACIÓN
FROM mcr.microsoft.com/dotnet/sdk:8.0-jammy AS build
WORKDIR /app

# Copiar archivos de proyecto y restaurar dependencias
COPY *.csproj ./
RUN dotnet restore

# Copiar todo el código y publicar
COPY . ./
RUN dotnet publish -c Release -o out /p:UseAppHost=false

# ETAPA DE EJECUCIÓN (Runtime)
FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy
WORKDIR /app
COPY --from=build /app/out .

# INSTALACIÓN DE LIBRERÍAS CRÍTICAS
# libicu y libssl son esenciales para que SQL Server no rompa la app en Linux
RUN apt-get update && apt-get install -y \
    libicu-dev \
    libssl-dev \
    && rm -rf /var/lib/apt/lists/*

# CONFIGURACIÓN DE ENTORNO
ENV ASPNETCORE_URLS=http://+:10000
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
EXPOSE 10000

ENTRYPOINT ["dotnet", "MercadoU.Api.dll"]