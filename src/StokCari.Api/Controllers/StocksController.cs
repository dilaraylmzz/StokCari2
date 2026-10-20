using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StokCari.Core.Constants;
using StokCari.Core.Dtos;
using StokCari.Core.Interfaces;

namespace StokCari.Api.Controllers;

[ApiController]
[Route("api/stocks")]
[Authorize(Roles = Roles.AdminOrUser)]
public class StocksController(IStockService service) : ControllerBase
{
    /// <summary>Sayfalı liste. Arama: stok kodu veya adı. SortBy: code, name, balance, unitPrice, createdAt.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<StockDto>>> List([FromQuery] PagedQuery query, CancellationToken ct) =>
        Ok(await service.ListAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StockDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await service.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<StockDto>> Create([FromBody] StockRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StockDto>> Update(Guid id, [FromBody] StockRequest request, CancellationToken ct) =>
        Ok(await service.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Saklanan bakiye ile geçerli (Approved) hareket toplamını karşılaştırır.</summary>
    [HttpGet("{id:guid}/balance-check")]
    public async Task<ActionResult<BalanceCheckDto>> CheckBalance(Guid id, CancellationToken ct) =>
        Ok(await service.CheckBalanceAsync(id, ct));
}
