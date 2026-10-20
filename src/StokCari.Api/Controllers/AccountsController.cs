using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StokCari.Core.Constants;
using StokCari.Core.Dtos;
using StokCari.Core.Interfaces;

namespace StokCari.Api.Controllers;

[ApiController]
[Route("api/accounts")]
[Authorize(Roles = Roles.AdminOrUser)]
public class AccountsController(IAccountService service) : ControllerBase
{
    /// <summary>Sayfalı liste. Arama: cari kodu veya adı. Type: Customer / Supplier. SortBy: code, name, balance, createdAt.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<AccountDto>>> List([FromQuery] AccountListQuery query, CancellationToken ct) =>
        Ok(await service.ListAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AccountDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await service.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<AccountDto>> Create([FromBody] AccountRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AccountDto>> Update(Guid id, [FromBody] AccountRequest request, CancellationToken ct) =>
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
