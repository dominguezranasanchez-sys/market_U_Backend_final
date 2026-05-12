using MercadoU.Api.Application.Interfaces;
using MercadoU.Api.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace MercadoU.Api.Controllers;

/// <summary>
/// Rutas consumidas por el frontend React (services/api.ts → ProductsAPI):
///   GET  /api/products                    → list(params)
///   GET  /api/products/{id}               → get(id)
///   POST /api/products                    → create(p)
///   GET  /api/products/{id}/images        → loadImages(productId)
///   POST /api/products/{id}/images        → addImage(productId, url, isPrimary)
///   GET  /api/users/{sellerId}/products   → bySeller(sellerId)  ← en UsersController
/// </summary>
[ApiController]
[Route("api/products")]
public sealed class ProductsController(IProductRepository repo) : ControllerBase
{
    // ------------------------------------------------------------------
    // GET /api/products?q=...&categoryId=...&locationId=...&minPrice=...
    // ------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ProductSearchParams search)
    {
        var products = await repo.SearchAsync(search);
        return Ok(products);
    }

    // ------------------------------------------------------------------
    // GET /api/products/{id}
    // ------------------------------------------------------------------
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var product = await repo.GetByIdAsync(id);
        if (product is null) return NotFound(new { message = "Producto no encontrado." });
        return Ok(product);
    }

    // ------------------------------------------------------------------
    // POST /api/products
    // Body: { title, description, price, categoryId, locationId, sellerId, condition, ... }
    // ------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest req)
    {
        if (req.Price <= 0)
            return BadRequest(new { message = "El precio debe ser mayor a 0." });

        var newId   = await repo.CreateAsync(req);
        var created = await repo.GetByIdAsync(newId);
        return CreatedAtAction(nameof(Get), new { id = newId }, created);
    }

    // ------------------------------------------------------------------
    // GET /api/products/{id}/images
    // ------------------------------------------------------------------
    [HttpGet("{id:int}/images")]
    public async Task<IActionResult> GetImages(int id)
    {
        var images = await repo.GetImagesAsync(id);
        return Ok(images);
    }

    // ------------------------------------------------------------------
    // POST /api/products/{id}/images
    // Body: { url: string, isPrimary: boolean }
    // ------------------------------------------------------------------
    [HttpPost("{id:int}/images")]
    public async Task<IActionResult> AddImage(int id, [FromBody] AddImageRequest req)
    {
        var image = await repo.AddImageAsync(id, req);
        return CreatedAtAction(nameof(GetImages), new { id }, image);
    }
}
