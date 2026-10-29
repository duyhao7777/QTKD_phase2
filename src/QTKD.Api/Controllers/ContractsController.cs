using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QTKD.Api.Infrastructure;

namespace QTKD.Api.Controllers;

[ApiController]
[Route("api/contracts")]
public class ContractsController(QtkdDbContext db) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var contract = await db.Contracts.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.ContractNo,
                x.CustomerId,
                CustomerName = x.Customer.Name,
                x.StartDate,
                x.EndDate,
                x.ServiceGroup,
                Status = x.Status.ToString()
            })
            .FirstOrDefaultAsync();

        return contract is null
            ? NotFound(new { error = "Không tìm thấy hợp đồng" })
            : Ok(contract);
    }
}