# Usa el SDK de .NET 8 para construir
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copiar el archivo de proyecto y restaurar dependencias
# Nota: Si tu archivo .csproj se llama exactamente MercadoU.Api.csproj esto funcionará
COPY *.csproj ./
RUN dotnet restore

# Copiar todo el código y publicar en la carpeta 'out'
COPY . ./
RUN dotnet publish -c Release -o out

# Imagen ligera para ejecutar la app
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .

# Obligar a la app a usar el puerto de Render
ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "MercadoU.Api.dll"]