using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Project.Infrastructure.Security;
using Project.Features.Veiculos.Commands;
using Project.Features.Veiculos.DTOs;
using Project.Features.Veiculos.Queries;

namespace Project.Features.Veiculos.Controllers;

[ApiController]
[Route("api/veiculos")]
public sealed class VeiculosController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any, NoStore = false)]
    public async Task<IActionResult> Buscar([FromQuery] BuscarVeiculosFiltroDto filtro, CancellationToken ct)
    {
        var result = await sender.Send(new BuscarVeiculosQuery(filtro), ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("{slug}")]
    [ResponseCache(Duration = 120, Location = ResponseCacheLocation.Any, NoStore = false)]
    public async Task<IActionResult> ObterPorSlug(string slug, CancellationToken ct)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var result = await sender.Send(new ObterVeiculoPorSlugQuery(slug, baseUrl), ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(result.Error);
    }

    [HttpPost]
    [Authorize(Policy = AdminAuthorization.MenuPolicyPrefix + "veiculos")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Criar([FromBody] VeiculoCreateDto dto, CancellationToken ct)
    {
        var result = await sender.Send(new CriarVeiculoCommand(dto), ct);
        return result.IsSuccess ? Ok(new { id = result.Value }) : BadRequest(result.Error);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = AdminAuthorization.MenuPolicyPrefix + "veiculos")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Atualizar(int id, [FromBody] VeiculoUpdateDto dto, CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("Id divergente.");
        var result = await sender.Send(new AtualizarVeiculoCommand(dto), ct);
        return result.IsSuccess ? NoContent() : BadRequest(result.Error);
    }

    [HttpPatch("{id:int}/vender")]
    [Authorize(Policy = AdminAuthorization.MenuPolicyPrefix + "vendas-veiculos")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarcarVendido(int id, CancellationToken ct)
    {
        var result = await sender.Send(new MarcarVeiculoComoVendidoCommand(id, DateTime.UtcNow), ct);
        return result.IsSuccess ? NoContent() : BadRequest(result.Error);
    }

    [HttpPost("{veiculoId:int}/midias")]
    [Authorize(Policy = AdminAuthorization.MenuPolicyPrefix + "veiculos")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> UploadMidias(int veiculoId, [FromForm] List<IFormFile> arquivos, [FromQuery] bool primeiraComoCapa = true, CancellationToken ct = default)
    {
        var dto = new VeiculoMidiaUploadDto { VeiculoId = veiculoId, DefinirPrimeiraComoCapa = primeiraComoCapa };
        var result = await sender.Send(new UploadMidiaVeiculoCommand(dto, arquivos), ct);
        return result.IsSuccess ? NoContent() : BadRequest(result.Error);
    }
}
