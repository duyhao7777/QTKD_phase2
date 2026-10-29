using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QTKD.Api.Infrastructure;

namespace QTKD.Api.Controllers;

[ApiController]
[Route("api/customers")]
public class CustomersController(QtkdDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await db.Customers.AsNoTracking()
            .Select(c => new { c.Id, c.Name, Status = c.Status.ToString() })
            .ToListAsync());
}