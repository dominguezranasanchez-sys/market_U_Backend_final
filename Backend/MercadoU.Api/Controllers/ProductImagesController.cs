using MercadoU.Api.Application.Interfaces;
using MercadoU.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MercadoU.Api.Controllers;

[ApiController]
[Route("api/products/{productId:int}/images")]
public sealed class ProductImagesController(IProductRepository repo) : ControllerBase
{
    // POST /api/products/{productId}/images/upload
    // multipart/form-data: campo "image" (IFormFile)
    // Query opcional: ?isPrimary=true
    [HttpPost("upload")]
    [Authorize]
    public async Task<IActionResult> Upload(
        int productId,
        IFormFile image,
        [FromQuery] bool isPrimary = false)
    {
        if (image is null || image.Length == 0)
            return BadRequest(new { message = "Imagen inválida." });

        // Validar que sea imagen
        var allowed = new[] { "image/jpeg", "image/png", "image/webp", "image/gif" };
        if (!allowed.Contains(image.ContentType.ToLowerInvariant()))
            return BadRequest(new { message = "Formato no soportado. Usa JPG, PNG o WebP." });

        // Guardar en wwwroot/uploads (carpeta persistente en Render via disco)
        var folder = Path.Combine("wwwroot", "uploads");
        Directory.CreateDirectory(folder);

        var ext      = Path.GetExtension(image.FileName);
        var fileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(folder, fileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
            await image.CopyToAsync(stream);

        // URL pública — el frontend la usará para mostrar la imagen
        var url = $"{Request.Scheme}://{Request.Host}/uploads/{fileName}";

        // Guardar en BD via repositorio existente
        var req    = new AddImageRequest(url, isPrimary);
        var saved  = await repo.AddImageAsync(productId, req);

        return Ok(new
        {
            productId = saved.ProductId,
            url       = saved.Url,
            isPrimary = saved.IsPrimary
        });
    }
}