using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StokCari.Core.Dtos;
using StokCari.Core.Interfaces;

namespace StokCari.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    /// <summary>Kullanıcı girişi. Dönen token "Authorization: Bearer ..." başlığıyla gönderilir.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken ct) =>
        Ok(await authService.LoginAsync(request, ct));
}
