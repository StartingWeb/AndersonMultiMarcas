using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project.Infrastructure.Security;

namespace Project.Controllers;

[ApiController]
[Route("api/security")]
[Authorize(Policy = AdminAuthorization.AccessPolicy)]
public sealed class SecurityController(IAntiforgery antiforgery) : ControllerBase
{
    [HttpGet("antiforgery")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Antiforgery()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { token = tokens.RequestToken, headerName = tokens.HeaderName });
    }
}
