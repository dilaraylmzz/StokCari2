using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StokCari.Core.Constants;
using StokCari.Core.Dtos;
using StokCari.Core.Enums;
using StokCari.Core.Interfaces;

namespace StokCari.Api.Controllers;

[ApiController]
[Route("api/receipt-series")]
[Authorize(Roles = Roles.AdminOrUser)]
public sealed class ReceiptSeriesController(IReceiptSeriesService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReceiptSeriesDto>>> List([FromQuery] ReceiptType? type, CancellationToken ct) =>
        Ok(await service.ListAsync(type, ct));

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ReceiptSeriesDto>> Create([FromBody] CreateReceiptSeriesRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(List), created);
    }
}
