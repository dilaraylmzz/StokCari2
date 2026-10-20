using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StokCari.Core.Constants;
using StokCari.Core.Dtos;
using StokCari.Core.Interfaces;

namespace StokCari.Api.Controllers;

[ApiController]
[Route("api/receipts")]
[Authorize(Roles = Roles.AdminOrUser)]
public class ReceiptsController(IReceiptService service) : ControllerBase
{
    /// <summary>Sayfalı liste. Filtreler: status, type, accountId, stockId, dateFrom, dateTo, search. SortBy: date, number, total.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<ReceiptDto>>> List([FromQuery] ReceiptListQuery query, CancellationToken ct) =>
        Ok(await service.ListAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReceiptDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await service.GetAsync(id, ct));

    /// <summary>Taslak (Draft) fiş oluşturur. Stok ve cari bakiyesi etkilenmez.</summary>
    [HttpPost]
    public async Task<ActionResult<ReceiptDto>> Create([FromBody] CreateReceiptRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Yalnızca taslak fiş güncellenebilir.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ReceiptDto>> Update(Guid id, [FromBody] UpdateReceiptRequest request, CancellationToken ct) =>
        Ok(await service.UpdateAsync(id, request, ct));

    /// <summary>Yalnızca taslak fiş silinebilir (soft delete).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Fişi onaylar: tek transaction'da stok/cari hareketleri üretilir, bakiyeler güncellenir.</summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<ReceiptDto>> Approve(Guid id, CancellationToken ct) =>
        Ok(await service.ApproveAsync(id, ct));

    /// <summary>Onaylı fişi iptal eder: bakiyeler geri alınır, hareketler geçersiz sayılır.</summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<ReceiptDto>> Cancel(Guid id, CancellationToken ct) =>
        Ok(await service.CancelAsync(id, ct));
}
