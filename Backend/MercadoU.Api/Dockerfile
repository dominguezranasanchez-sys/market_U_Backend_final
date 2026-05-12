# Etapa de compilación
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar archivos de proyecto y restaurar
COPY ["MercadoU.Api.csproj", "./"]
RUN dotnet restore "./MercadoU.Api.csproj"

# Copiar el resto del código y publicar
COPY . .
RUN dotnet publish "MercadoU.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Etapa final (Runtime)
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Instalar librerías necesarias para SQL Server en Linux (Evita el error 139)
RUN apt-get update && apt-get install -y libicu-dev libssl-dev && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Forzar puerto de Render
ENV ASPNETCORE_URLS=http://0.0.0.0:10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "MercadoU.Api.dll"]