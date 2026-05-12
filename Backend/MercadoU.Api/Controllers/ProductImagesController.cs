using Microsoft.AspNetCore.Mvc;

namespace MercadoU.Api.Controllers;

[ApiController]
[Route("api/products/{productId}/images")]
public class ProductImagesController : ControllerBase
{
    [HttpPost("upload")]
    public async Task<IActionResult> Upload(
        int productId,
        IFormFile image)
    {
        if (image == null || image.Length == 0)
            return BadRequest("Imagen inválida.");

        var fileName = $"{Guid.NewGuid()}_{image.FileName}";
        var folder = Path.Combine("wwwroot", "uploads");

        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, fileName);

        using var stream = new FileStream(path, FileMode.Create);
        await image.CopyToAsync(stream);

        return Ok(new
        {
            id = 1,
            productId,
            imageUrl = $"/uploads/{fileName}"
        });
    }
}