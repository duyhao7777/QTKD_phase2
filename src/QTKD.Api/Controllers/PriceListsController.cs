using Microsoft.AspNetCore.Mvc;
using QTKD.Api.Application;

namespace QTKD.Api.Controllers;

[ApiController]
[Route("api/pricelists")]
public class PriceListsController(PriceListService svc) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct) => Ok(await svc.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) => Ok(await svc.GetAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(PriceListCreateRequest request, CancellationToken ct)
    {
        var result = await svc.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}/items")]
    public async Task<IActionResult> GetItems(Guid id, CancellationToken ct) => Ok(await svc.GetItemsAsync(id, ct));

    [HttpPost("{id:guid}/items")]
    public async Task<IActionResult> AddItem(Guid id, PriceItemRequest request, CancellationToken ct) =>
        Ok(await svc.AddItemAsync(id, request, ct));
}