// ARCHIVO ELIMINADO — la ruta GET/POST /api/products/{id}/images
// fue consolidada en ProductsController.cs para evitar conflicto de rutas
// y el error 500 causado por ambiguedad en el routing de ASP.NET Core.
//
// El upload de archivos (POST /api/products/{productId}/images/upload) tampoco
// se usa: el frontend sube URLs externas, no archivos binarios.
// Si en el futuro se necesita, reimplementar con inyección de IProductRepository.

namespace MercadoU.Api.Controllers;
// (vacío intencionalmente)
