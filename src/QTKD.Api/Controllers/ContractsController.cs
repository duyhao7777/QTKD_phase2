using Microsoft.AspNetCore.Mvc;
using QTKD.Api.Application;

namespace QTKD.Api.Controllers;

[ApiController]
[Route("api/contracts")]
public class ContractsController(ContractService svc) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct) => Ok(await svc.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) => Ok(await svc.GetAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(ContractCreateRequest request, CancellationToken ct)
    {
        var result = await svc.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, ContractUpdateRequest request, CancellationToken ct) =>
        Ok(await svc.UpdateAsync(id, request, ct));

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, [FromBody] SubmitRequest? body, CancellationToken ct)
    {
        var key = Request.Headers["Idempotency-Key"].FirstOrDefault();
        var result = await svc.SubmitAsync(id, UserContext.From(Request), key, body?.Comment, ct);
        return Ok(result);
    }
}