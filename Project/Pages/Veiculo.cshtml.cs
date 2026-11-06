using Core.Storage;
using Data;
using Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Project.Infrastructure.Storage;
using Project.Shared;
using Project.Pages.ViewModels;

namespace Project.Pages;

public sealed class VeiculoModel(
    ApplicationDbContext db,
    IStorageImageResolver imageResolver) : PageModel
{
    private static readonly IReadOnlyDictionary<TipoVeiculoOpcional, string> OpcionalLabels = new Dictionary<TipoVeiculoOpcional, string>
    {
        [TipoVeiculoOpcional.ArCondicionado] = "Ar-condicionado",
        [TipoVeiculoOpcional.ArQuente] = "Ar quente",
        [TipoVeiculoOpcional.DirecaoHidraulica] = "Direcao hidraulica",
        [TipoVeiculoOpcional.DirecaoEletrica] = "Direcao eletrica",
        [TipoVeiculoOpcional.VidroEletrico] = "Vidro eletrico",
        [TipoVeiculoOpcional.TravaEletrica] = "Trava eletrica",
        [TipoVeiculoOpcional.RetrovisorEletrico] = "Retrovisor eletrico",
        [TipoVeiculoOpcional.BancoDeCouro] = "Banco de couro",
        [TipoVeiculoOpcional.AjusteEletricoBancos] = "Ajuste eletrico dos bancos",
        [TipoVeiculoOpcional.AquecimentoBancos] = "Aquecimento dos bancos",
        [TipoVeiculoOpcional.VolanteMultifuncional] = "Volante multifuncional",
        [TipoVeiculoOpcional.PilotoAutomatico] = "Piloto automatico",
        [TipoVeiculoOpcional.ControleAutomaticoVelocidade] = "Controle automatico de velocidade",
        [TipoVeiculoOpcional.LimitadorVelocidade] = "Limitador de velocidade",
        [TipoVeiculoOpcional.ComputadorBordo] = "Computador de bordo",
        [TipoVeiculoOpcional.ChavePresencial] = "Chave presencial",
        [TipoVeiculoOpcional.PartidaBotao] = "Partida por botao",
        [TipoVeiculoOpcional.SensorChuva] = "Sensor de chuva",
        [TipoVeiculoOpcional.SensorCrepuscular] = "Sensor crepuscular",
        [TipoVeiculoOpcional.TetoSolar] = "Teto solar",
        [TipoVeiculoOpcional.TetoPanoramico] = "Teto panoramico",
        [TipoVeiculoOpcional.AirbagMotorista] = "Airbag motorista",
        [TipoVeiculoOpcional.AirbagPassageiro] = "Airbag passageiro",
        [TipoVeiculoOpcional.AirbagLateral] = "Airbag lateral",
        [TipoVeiculoOpcional.AirbagCortina] = "Airbag cortina",
        [TipoVeiculoOpcional.FreiosAbs] = "Freios ABS",
        [TipoVeiculoOpcional.ControleTracao] = "Controle de tracao",
        [TipoVeiculoOpcional.ControleEstabilidade] = "Controle de estabilidade",
        [TipoVeiculoOpcional.AssistentePartidaRampa] = "Assistente de partida em rampa",
        [TipoVeiculoOpcional.Isofix] = "Isofix",
        [TipoVeiculoOpcional.Alarme] = "Alarme",
        [TipoVeiculoOpcional.CameraDeRe] = "Camera de re",
        [TipoVeiculoOpcional.SensorEstacionamentoDianteiro] = "Sensor dianteiro",
        [TipoVeiculoOpcional.SensorEstacionamentoTraseiro] = "Sensor traseiro",
        [TipoVeiculoOpcional.FarolNeblina] = "Farol de neblina",
        [TipoVeiculoOpcional.FarolLed] = "Farol LED",
        [TipoVeiculoOpcional.FarolMilha] = "Farol de milha",
        [TipoVeiculoOpcional.CentralMultimidia] = "Central multimidia",
        [TipoVeiculoOpcional.Som] = "Som",
        [TipoVeiculoOpcional.Bluetooth] = "Bluetooth",
        [TipoVeiculoOpcional.Usb] = "USB",
        [TipoVeiculoOpcional.EntradaAuxiliar] = "Entrada auxiliar",
        [TipoVeiculoOpcional.Radio] = "Radio",
        [TipoVeiculoOpcional.GPS] = "GPS",
        [TipoVeiculoOpcional.CarregadorInducao] = "Carregador por inducao",
        [TipoVeiculoOpcional.AppleCarPlay] = "Apple CarPlay",
        [TipoVeiculoOpcional.AndroidAuto] = "Android Auto",
        [TipoVeiculoOpcional.RodaLigaLeve] = "Roda de liga leve",
        [TipoVeiculoOpcional.KitMultimidia] = "Kit multimidia",
        [TipoVeiculoOpcional.Engate] = "Engate",
        [TipoVeiculoOpcional.Bagageiro] = "Bagageiro",
        [TipoVeiculoOpcional.CapotaMaritima] = "Capota maritima",
        [TipoVeiculoOpcional.Estribo] = "Estribo",
        [TipoVeiculoOpcional.SantoAntonio] = "Santo Antonio",
        [TipoVeiculoOpcional.ProtetorCacamba] = "Protetor de cacamba",
        [TipoVeiculoOpcional.PortaMalasEletrico] = "Porta-malas eletrico",
        [TipoVeiculoOpcional.TerceiraFileira] = "Terceira fileira",
        [TipoVeiculoOpcional.CambioAutomatico] = "Cambio automatico",
        [TipoVeiculoOpcional.CambioManual] = "Cambio manual",
        [TipoVeiculoOpcional.CambioCvt] = "Cambio CVT",
        [TipoVeiculoOpcional.CambioAutomatizado] = "Cambio automatizado",
        [TipoVeiculoOpcional.TracaoDianteira] = "Tracao dianteira",
        [TipoVeiculoOpcional.TracaoTraseira] = "Tracao traseira",
        [TipoVeiculoOpcional.TracaoIntegral] = "Tracao integral",
        [TipoVeiculoOpcional.StartStop] = "Start-stop",
        [TipoVeiculoOpcional.Turbo] = "Turbo",
        [TipoVeiculoOpcional.Hibrido] = "Hibrido",
        [TipoVeiculoOpcional.Eletrico] = "Eletrico"
    };

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    public VehicleDetailViewModel Vehicle { get; private set; } = null!;
    public IReadOnlyCollection<HomeSellerViewModel> Vendedores { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var veiculo = await db.Veiculos
            .AsNoTracking()
            .Where(x => x.Id == Id && x.Ativo && !x.Vendido)
            .Select(x => new
            {
                x.Id,
                x.Titulo,
                x.Modelo,
                x.Versao,
                x.AnoFabricacao,
                x.AnoModelo,
                x.Cor,
                x.Cambio,
                x.Combustivel,
                x.Quilometragem,
                x.PrecoVenda,
                x.Descricao,
                x.UrlVideo,
                x.AceitaTroca,
                x.Financiavel,
                MarcaNome = x.Marca.Nome,
                LojaNome = x.Loja.Nome
            })
            .FirstOrDefaultAsync(ct);

        if (veiculo is null)
        {
            return NotFound();
        }

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE [Veiculo]
            SET [QuantidadeVisualizacoes] = COALESCE([QuantidadeVisualizacoes], 0) + 1
            WHERE [Id] = {veiculo.Id}
            """,
            ct);

        var vendedores = await db.Vendedores
            .AsNoTracking()
            .Where(x => x.Ativo)
            .OrderBy(x => x.Nome)
            .Select(x => new SellerProjection
            {
                Nome = x.Nome,
                Telefone = x.Whatsapp.HasValue ? x.Whatsapp.Value.Valor : (x.Telefone.HasValue ? x.Telefone.Value.Valor : string.Empty),
                FotoUrl = x.FotoUrl
            })
            .Take(12)
            .ToListAsync(ct);

        Vendedores = ToSellerViewModels(vendedores);

        var nomeCompleto = BuildNomeCompleto(veiculo.Titulo, veiculo.Modelo, veiculo.Versao);
        var medias = new List<VehiclePhotoViewModel>
        {
            new()
            {
                Url = VehicleImageHelper.DefaultVehicleImage,
                Alt = nomeCompleto,
                Width = 1280,
                Height = 960
            }
        };

        var vehicleMedia = await db.VeiculoMidias
            .AsNoTracking()
            .Where(x => x.Ativo && x.VeiculoId == veiculo.Id && x.Tipo == TipoMidia.Imagem)
            .OrderByDescending(x => x.Capa)
            .ThenBy(x => x.Ordem)
            .Select(x => new MediaProjection
            {
                Url = x.Url,
                BlobName = x.BlobName,
                Container = x.Container,
                NomeArquivo = x.NomeArquivo,
                ContentType = x.ContentType,
                TamanhoBytes = x.TamanhoBytes
            })
            .ToListAsync(ct);

        if (vehicleMedia.Count > 0)
        {
            medias = imageResolver.ResolveVehicleGallery(vehicleMedia.Select(ToStorageReference), includeDefault: true)
                .Select(x => new VehiclePhotoViewModel
                {
                    Url = x,
                    Alt = string.Equals(x, VehicleImageHelper.DefaultVehicleImage, StringComparison.OrdinalIgnoreCase)
                        ? nomeCompleto
                        : $"{nomeCompleto} a venda na Anderson Multimarcas",
                    Width = 1280,
                    Height = 960
                })
                .ToList();
        }

        var caracteristicas = await db.VeiculoCaracteristicas
            .AsNoTracking()
            .Where(x => x.VeiculoId == veiculo.Id)
            .Select(x => new VehicleFeaturesProjection
            {
                ArCondicionado = x.ArCondicionado,
                ArQuente = x.ArQuente,
                DirecaoHidraulica = x.DirecaoHidraulica,
                DirecaoEletrica = x.DirecaoEletrica,
                VidroEletrico = x.VidroEletrico,
                TravaEletrica = x.TravaEletrica,
                RetrovisorEletrico = x.RetrovisorEletrico,
                BancoDeCouro = x.BancoDeCouro,
                AjusteEletricoBancos = x.AjusteEletricoBancos,
                AquecimentoBancos = x.AquecimentoBancos,
                VolanteMultifuncional = x.VolanteMultifuncional,
                PilotoAutomatico = x.PilotoAutomatico,
                ControleAutomaticoVelocidade = x.ControleAutomaticoVelocidade,
                LimitadorVelocidade = x.LimitadorVelocidade,
                ComputadorBordo = x.ComputadorBordo,
                ChavePresencial = x.ChavePresencial,
                PartidaBotao = x.PartidaBotao,
                SensorChuva = x.SensorChuva,
                SensorCrepuscular = x.SensorCrepuscular,
                TetoSolar = x.TetoSolar,
                TetoPanoramico = x.TetoPanoramico,
                AirbagMotorista = x.AirbagMotorista,
                AirbagPassageiro = x.AirbagPassageiro,
                AirbagLateral = x.AirbagLateral,
                AirbagCortina = x.AirbagCortina,
                FreiosAbs = x.FreiosAbs,
                ControleTracao = x.ControleTracao,
                ControleEstabilidade = x.ControleEstabilidade,
                AssistentePartidaRampa = x.AssistentePartidaRampa,
                Isofix = x.Isofix,
                Alarme = x.Alarme,
                CameraDeRe = x.CameraDeRe,
                SensorEstacionamentoDianteiro = x.SensorEstacionamentoDianteiro,
                SensorEstacionamentoTraseiro = x.SensorEstacionamentoTraseiro,
                FarolNeblina = x.FarolNeblina,
                FarolLed = x.FarolLed,
                FarolMilha = x.FarolMilha,
                CentralMultimidia = x.CentralMultimidia,
                Som = x.Som,
                Bluetooth = x.Bluetooth,
                Usb = x.Usb,
                EntradaAuxiliar = x.EntradaAuxiliar,
                Radio = x.Radio,
                Gps = x.GPS,
                CarregadorInducao = x.CarregadorInducao,
                AppleCarPlay = x.AppleCarPlay,
                AndroidAuto = x.AndroidAuto,
                RodaLigaLeve = x.RodaLigaLeve,
                KitMultimidia = x.KitMultimidia,
                Engate = x.Engate,
                Bagageiro = x.Bagageiro,
                CapotaMaritima = x.CapotaMaritima,
                Estribo = x.Estribo,
                SantoAntonio = x.SantoAntonio,
                ProtetorCacamba = x.ProtetorCacamba,
                PortaMalasEletrico = x.PortaMalasEletrico,
                TerceiraFileira = x.TerceiraFileira,
                CambioAutomatico = x.CambioAutomatico,
                CambioManual = x.CambioManual,
                CambioCvt = x.CambioCvt,
                CambioAutomatizado = x.CambioAutomatizado,
                TracaoDianteira = x.TracaoDianteira,
                TracaoTraseira = x.TracaoTraseira,
                TracaoIntegral = x.TracaoIntegral,
                StartStop = x.StartStop,
                Turbo = x.Turbo,
                Hibrido = x.Hibrido,
                Eletrico = x.Eletrico
            })
            .FirstOrDefaultAsync(ct);

        var opcionais = BuildOpcionais(caracteristicas);

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var canonicalUrl = $"{baseUrl}/veiculo/{veiculo.Id}/";
        var seoTitle = $"{nomeCompleto} - Anderson Multimarcas";
        var seoDescription = $"{veiculo.MarcaNome} {veiculo.Modelo} {veiculo.AnoModelo} {(veiculo.PrecoVenda.Valor > 0 ? $"por {veiculo.PrecoVenda.Valor:C}" : "disponivel para consulta")}.";

        Vehicle = new VehicleDetailViewModel
        {
            Id = veiculo.Id,
            Titulo = veiculo.Titulo,
            Modelo = veiculo.Modelo,
            NomeCompleto = nomeCompleto,
            Versao = veiculo.Versao,
            Marca = veiculo.MarcaNome,
            Loja = veiculo.LojaNome,
            Cor = string.IsNullOrWhiteSpace(veiculo.Cor) ? "Não informado" : veiculo.Cor,
            AnoFabricacao = veiculo.AnoFabricacao,
            AnoModelo = veiculo.AnoModelo,
            Cambio = FormatCambio(veiculo.Cambio),
            Combustivel = FormatCombustivel(veiculo.Combustivel),
            Quilometragem = veiculo.Quilometragem,
            PrecoVenda = veiculo.PrecoVenda.Valor,
            Descricao = string.IsNullOrWhiteSpace(veiculo.Descricao)
                ? "Consulte nossa equipe para receber mais detalhes sobre esse veículo."
                : veiculo.Descricao.Trim(),
            UrlVideo = veiculo.UrlVideo,
            AceitaTroca = veiculo.AceitaTroca,
            Financiavel = veiculo.Financiavel,
            Fotos = medias,
            Opcionais = opcionais
        };

        ViewData["Title"] = Vehicle.NomeCompleto;
        ViewData["SeoTitle"] = seoTitle;
        ViewData["MetaDescription"] = seoDescription;
        ViewData["CanonicalUrl"] = canonicalUrl;
        ViewData["OgImage"] = medias[0].Url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? medias[0].Url
            : $"{baseUrl}{medias[0].Url}";
        ViewData["Robots"] = "index,follow";

        ViewData["BreadcrumbSchema"] = SeoJsonLd.Breadcrumb(baseUrl,
            ("Inicio", "/"),
            ("Estoque", "/veiculos"),
            (Vehicle.NomeCompleto, canonicalUrl));

        ViewData["VehicleSchema"] = SeoJsonLd.Vehicle(
            Vehicle.NomeCompleto, Vehicle.Marca, Vehicle.AnoModelo, Vehicle.Cor,
            Vehicle.Combustivel, Vehicle.Cambio,
            medias.Select(x => x.Url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? x.Url : $"{baseUrl}{x.Url}"),
            Vehicle.PrecoVenda, canonicalUrl);

        return Page();
    }

    public async Task<IActionResult> OnGetRegistrarCliqueAsync(int id, CancellationToken ct)
    {
        if (id <= 0)
        {
            return new EmptyResult();
        }

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE [Veiculo]
            SET [QuantidadeCliques] = COALESCE([QuantidadeCliques], 0) + 1
            WHERE [Id] = {id}
            """,
            ct);

        return new EmptyResult();
    }

    private static string BuildNomeCompleto(string titulo, string modelo, string? versao)
    {
        var tituloLimpo = (titulo ?? string.Empty).Trim();
        var modeloLimpo = (modelo ?? string.Empty).Trim();
        var versaoLimpa = (versao ?? string.Empty).Trim();

        var incluirModelo = !string.IsNullOrWhiteSpace(modeloLimpo)
            && !string.Equals(tituloLimpo, modeloLimpo, StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(versaoLimpa))
        {
            return incluirModelo ? $"{tituloLimpo} {modeloLimpo}" : tituloLimpo;
        }

        return incluirModelo
            ? $"{tituloLimpo} {modeloLimpo} {versaoLimpa}"
            : $"{tituloLimpo} {versaoLimpa}";
    }

    private static List<string> BuildOpcionais(VehicleFeaturesProjection? features)
    {
        if (features is null)
        {
            return [];
        }

        var opcionais = new List<string>();

        void AddIf(bool ativo, TipoVeiculoOpcional opcional)
        {
            if (ativo && OpcionalLabels.TryGetValue(opcional, out var label))
            {
                opcionais.Add(label);
            }
        }

        AddIf(features.ArCondicionado, TipoVeiculoOpcional.ArCondicionado);
        AddIf(features.ArQuente, TipoVeiculoOpcional.ArQuente);
        AddIf(features.DirecaoHidraulica, TipoVeiculoOpcional.DirecaoHidraulica);
        AddIf(features.DirecaoEletrica, TipoVeiculoOpcional.DirecaoEletrica);
        AddIf(features.VidroEletrico, TipoVeiculoOpcional.VidroEletrico);
        AddIf(features.TravaEletrica, TipoVeiculoOpcional.TravaEletrica);
        AddIf(features.RetrovisorEletrico, TipoVeiculoOpcional.RetrovisorEletrico);
        AddIf(features.BancoDeCouro, TipoVeiculoOpcional.BancoDeCouro);
        AddIf(features.AjusteEletricoBancos, TipoVeiculoOpcional.AjusteEletricoBancos);
        AddIf(features.AquecimentoBancos, TipoVeiculoOpcional.AquecimentoBancos);
        AddIf(features.VolanteMultifuncional, TipoVeiculoOpcional.VolanteMultifuncional);
        AddIf(features.PilotoAutomatico, TipoVeiculoOpcional.PilotoAutomatico);
        AddIf(features.ControleAutomaticoVelocidade, TipoVeiculoOpcional.ControleAutomaticoVelocidade);
        AddIf(features.LimitadorVelocidade, TipoVeiculoOpcional.LimitadorVelocidade);
        AddIf(features.ComputadorBordo, TipoVeiculoOpcional.ComputadorBordo);
        AddIf(features.ChavePresencial, TipoVeiculoOpcional.ChavePresencial);
        AddIf(features.PartidaBotao, TipoVeiculoOpcional.PartidaBotao);
        AddIf(features.SensorChuva, TipoVeiculoOpcional.SensorChuva);
        AddIf(features.SensorCrepuscular, TipoVeiculoOpcional.SensorCrepuscular);
        AddIf(features.TetoSolar, TipoVeiculoOpcional.TetoSolar);
        AddIf(features.TetoPanoramico, TipoVeiculoOpcional.TetoPanoramico);
        AddIf(features.AirbagMotorista, TipoVeiculoOpcional.AirbagMotorista);
        AddIf(features.AirbagPassageiro, TipoVeiculoOpcional.AirbagPassageiro);
        AddIf(features.AirbagLateral, TipoVeiculoOpcional.AirbagLateral);
        AddIf(features.AirbagCortina, TipoVeiculoOpcional.AirbagCortina);
        AddIf(features.FreiosAbs, TipoVeiculoOpcional.FreiosAbs);
        AddIf(features.ControleTracao, TipoVeiculoOpcional.ControleTracao);
        AddIf(features.ControleEstabilidade, TipoVeiculoOpcional.ControleEstabilidade);
        AddIf(features.AssistentePartidaRampa, TipoVeiculoOpcional.AssistentePartidaRampa);
        AddIf(features.Isofix, TipoVeiculoOpcional.Isofix);
        AddIf(features.Alarme, TipoVeiculoOpcional.Alarme);
        AddIf(features.CameraDeRe, TipoVeiculoOpcional.CameraDeRe);
        AddIf(features.SensorEstacionamentoDianteiro, TipoVeiculoOpcional.SensorEstacionamentoDianteiro);
        AddIf(features.SensorEstacionamentoTraseiro, TipoVeiculoOpcional.SensorEstacionamentoTraseiro);
        AddIf(features.FarolNeblina, TipoVeiculoOpcional.FarolNeblina);
        AddIf(features.FarolLed, TipoVeiculoOpcional.FarolLed);
        AddIf(features.FarolMilha, TipoVeiculoOpcional.FarolMilha);
        AddIf(features.CentralMultimidia, TipoVeiculoOpcional.CentralMultimidia);
        AddIf(features.Som, TipoVeiculoOpcional.Som);
        AddIf(features.Bluetooth, TipoVeiculoOpcional.Bluetooth);
        AddIf(features.Usb, TipoVeiculoOpcional.Usb);
        AddIf(features.EntradaAuxiliar, TipoVeiculoOpcional.EntradaAuxiliar);
        AddIf(features.Radio, TipoVeiculoOpcional.Radio);
        AddIf(features.Gps, TipoVeiculoOpcional.GPS);
        AddIf(features.CarregadorInducao, TipoVeiculoOpcional.CarregadorInducao);
        AddIf(features.AppleCarPlay, TipoVeiculoOpcional.AppleCarPlay);
        AddIf(features.AndroidAuto, TipoVeiculoOpcional.AndroidAuto);
        AddIf(features.RodaLigaLeve, TipoVeiculoOpcional.RodaLigaLeve);
        AddIf(features.KitMultimidia, TipoVeiculoOpcional.KitMultimidia);
        AddIf(features.Engate, TipoVeiculoOpcional.Engate);
        AddIf(features.Bagageiro, TipoVeiculoOpcional.Bagageiro);
        AddIf(features.CapotaMaritima, TipoVeiculoOpcional.CapotaMaritima);
        AddIf(features.Estribo, TipoVeiculoOpcional.Estribo);
        AddIf(features.SantoAntonio, TipoVeiculoOpcional.SantoAntonio);
        AddIf(features.ProtetorCacamba, TipoVeiculoOpcional.ProtetorCacamba);
        AddIf(features.PortaMalasEletrico, TipoVeiculoOpcional.PortaMalasEletrico);
        AddIf(features.TerceiraFileira, TipoVeiculoOpcional.TerceiraFileira);
        AddIf(features.CambioAutomatico, TipoVeiculoOpcional.CambioAutomatico);
        AddIf(features.CambioManual, TipoVeiculoOpcional.CambioManual);
        AddIf(features.CambioCvt, TipoVeiculoOpcional.CambioCvt);
        AddIf(features.CambioAutomatizado, TipoVeiculoOpcional.CambioAutomatizado);
        AddIf(features.TracaoDianteira, TipoVeiculoOpcional.TracaoDianteira);
        AddIf(features.TracaoTraseira, TipoVeiculoOpcional.TracaoTraseira);
        AddIf(features.TracaoIntegral, TipoVeiculoOpcional.TracaoIntegral);
        AddIf(features.StartStop, TipoVeiculoOpcional.StartStop);
        AddIf(features.Turbo, TipoVeiculoOpcional.Turbo);
        AddIf(features.Hibrido, TipoVeiculoOpcional.Hibrido);
        AddIf(features.Eletrico, TipoVeiculoOpcional.Eletrico);

        return opcionais;
    }

    private static string FormatCambio(Cambio cambio) => cambio switch
    {
        Cambio.Automatico => "Automatico",
        Cambio.Manual => "Manual",
        Cambio.Cvt => "CVT",
        Cambio.Automatizado => "Automatizado",
        _ => "Não informado"
    };

    private static string FormatCombustivel(Combustivel combustivel) => combustivel switch
    {
        Combustivel.Gasolina => "Gasolina",
        Combustivel.Etanol => "Etanol",
        Combustivel.Flex => "Flex",
        Combustivel.Diesel => "Diesel",
        Combustivel.Gnv => "GNV",
        Combustivel.Hibrido => "Hibrido",
        Combustivel.Eletrico => "Eletrico",
        _ => "Não informado"
    };

    private IReadOnlyList<HomeSellerViewModel> ToSellerViewModels(IEnumerable<SellerProjection> sellers)
    {
        var result = new List<HomeSellerViewModel>();
        foreach (var seller in sellers)
        {
            result.Add(new HomeSellerViewModel
            {
                Nome = seller.Nome,
                Telefone = seller.Telefone,
                FotoUrl = imageResolver.ResolveSellerPhoto(seller.FotoUrl)
            });
        }

        return result;
    }

    private static StorageImageReference ToStorageReference(MediaProjection media)
        => new(media.Url, media.BlobName, media.Container, media.NomeArquivo, media.ContentType, media.TamanhoBytes);

    public sealed class VehicleDetailViewModel
    {
        public int Id { get; init; }
        public string Titulo { get; init; } = string.Empty;
        public string Modelo { get; init; } = string.Empty;
        public string NomeCompleto { get; init; } = string.Empty;
        public string? Versao { get; init; }
        public string Marca { get; init; } = string.Empty;
        public string Loja { get; init; } = string.Empty;
        public string Cor { get; init; } = string.Empty;
        public int? AnoFabricacao { get; init; }
        public int AnoModelo { get; init; }
        public string Cambio { get; init; } = string.Empty;
        public string Combustivel { get; init; } = string.Empty;
        public int? Quilometragem { get; init; }
        public decimal PrecoVenda { get; init; }
        public string Descricao { get; init; } = string.Empty;
        public string? UrlVideo { get; init; }
        public bool AceitaTroca { get; init; }
        public bool Financiavel { get; init; }
        public IReadOnlyList<VehiclePhotoViewModel> Fotos { get; init; } = [];
        public IReadOnlyList<string> Opcionais { get; init; } = [];
    }

    public sealed class VehiclePhotoViewModel
    {
        public string Url { get; init; } = string.Empty;
        public string Alt { get; init; } = string.Empty;
        public int Width { get; init; }
        public int Height { get; init; }
    }

    private sealed class SellerProjection
    {
        public string Nome { get; init; } = string.Empty;
        public string Telefone { get; init; } = string.Empty;
        public string? FotoUrl { get; init; }
    }

    private sealed class MediaProjection
    {
        public string? Url { get; init; }
        public string? BlobName { get; init; }
        public string? Container { get; init; }
        public string? NomeArquivo { get; init; }
        public string? ContentType { get; init; }
        public long? TamanhoBytes { get; init; }
    }

    private sealed class VehicleFeaturesProjection
    {
        public bool ArCondicionado { get; init; }
        public bool ArQuente { get; init; }
        public bool DirecaoHidraulica { get; init; }
        public bool DirecaoEletrica { get; init; }
        public bool VidroEletrico { get; init; }
        public bool TravaEletrica { get; init; }
        public bool RetrovisorEletrico { get; init; }
        public bool BancoDeCouro { get; init; }
        public bool AjusteEletricoBancos { get; init; }
        public bool AquecimentoBancos { get; init; }
        public bool VolanteMultifuncional { get; init; }
        public bool PilotoAutomatico { get; init; }
        public bool ControleAutomaticoVelocidade { get; init; }
        public bool LimitadorVelocidade { get; init; }
        public bool ComputadorBordo { get; init; }
        public bool ChavePresencial { get; init; }
        public bool PartidaBotao { get; init; }
        public bool SensorChuva { get; init; }
        public bool SensorCrepuscular { get; init; }
        public bool TetoSolar { get; init; }
        public bool TetoPanoramico { get; init; }
        public bool AirbagMotorista { get; init; }
        public bool AirbagPassageiro { get; init; }
        public bool AirbagLateral { get; init; }
        public bool AirbagCortina { get; init; }
        public bool FreiosAbs { get; init; }
        public bool ControleTracao { get; init; }
        public bool ControleEstabilidade { get; init; }
        public bool AssistentePartidaRampa { get; init; }
        public bool Isofix { get; init; }
        public bool Alarme { get; init; }
        public bool CameraDeRe { get; init; }
        public bool SensorEstacionamentoDianteiro { get; init; }
        public bool SensorEstacionamentoTraseiro { get; init; }
        public bool FarolNeblina { get; init; }
        public bool FarolLed { get; init; }
        public bool FarolMilha { get; init; }
        public bool CentralMultimidia { get; init; }
        public bool Som { get; init; }
        public bool Bluetooth { get; init; }
        public bool Usb { get; init; }
        public bool EntradaAuxiliar { get; init; }
        public bool Radio { get; init; }
        public bool Gps { get; init; }
        public bool CarregadorInducao { get; init; }
        public bool AppleCarPlay { get; init; }
        public bool AndroidAuto { get; init; }
        public bool RodaLigaLeve { get; init; }
        public bool KitMultimidia { get; init; }
        public bool Engate { get; init; }
        public bool Bagageiro { get; init; }
        public bool CapotaMaritima { get; init; }
        public bool Estribo { get; init; }
        public bool SantoAntonio { get; init; }
        public bool ProtetorCacamba { get; init; }
        public bool PortaMalasEletrico { get; init; }
        public bool TerceiraFileira { get; init; }
        public bool CambioAutomatico { get; init; }
        public bool CambioManual { get; init; }
        public bool CambioCvt { get; init; }
        public bool CambioAutomatizado { get; init; }
        public bool TracaoDianteira { get; init; }
        public bool TracaoTraseira { get; init; }
        public bool TracaoIntegral { get; init; }
        public bool StartStop { get; init; }
        public bool Turbo { get; init; }
        public bool Hibrido { get; init; }
        public bool Eletrico { get; init; }
    }
}
