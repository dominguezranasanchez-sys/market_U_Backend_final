# Etapa de compilación
FROM mcr.microsoft.com/dotnet/sdk:8.0-jammy AS build
WORKDIR /app

# Copiar archivos y restaurar
COPY *.csproj ./
RUN dotnet restore

# Publicar
COPY . ./
RUN dotnet publish -c Release -o out /p:UseAppHost=false

# Etapa de ejecución (Usando Ubuntu Jammy para evitar el error 139)
FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy
WORKDIR /app
COPY --from=build /app/out .

# Instalar dependencias necesarias para SQL Server y Globalización
RUN apt-get update && apt-get install -y libicu-dev libssl-dev && rm -rf /var/lib/apt/lists/*

# Configuración de puerto para Render
ENV ASPNETCORE_URLS=http://+:10000
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
EXPOSE 10000

ENTRYPOINT ["dotnet", "MercadoU.Api.dll"]