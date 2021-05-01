using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.ServiceBus;
using APIAcoes.Models;
using APIAcoes.Data;

namespace APIAcoes.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AcoesController : ControllerBase
    {
        private readonly ILogger<AcoesController> _logger;
        private readonly AcoesRepository _repository;

        public AcoesController(ILogger<AcoesController> logger,
            AcoesRepository repository)
        {
            _logger = logger;
             _repository = repository;            
        }

        [HttpPost]
        [ProducesResponseType(typeof(Resultado), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<Resultado> Post(
            [FromServices] IConfiguration configuration,
            Acao acao)
        {
            CotacaoAcao cotacaoAcao = new ()
            {
                Codigo = acao.Codigo,
                Valor = acao.Valor,
                CodCorretora = configuration["Corretora:Codigo"],
                NomeCorretora = configuration["Corretora:Nome"]
            };
            var conteudoAcao = JsonSerializer.Serialize(cotacaoAcao);
            _logger.LogInformation($"Dados: {conteudoAcao}");

            string topic = configuration["AzureServiceBus:Topic"];
            var client = new TopicClient(
                configuration["AzureServiceBus:ConnectionString"], topic);
            await client.SendAsync(
                new Message(Encoding.UTF8.GetBytes(conteudoAcao)));
            _logger.LogInformation(
                $"Azure Service Bus - Envio para o tópico {conteudoAcao} concluído");

            return new ()
            {
                Mensagem = "Informações de ação enviadas com sucesso!"
            };
        }

        [HttpGet("{codigo}")]
        [ProducesResponseType(typeof(UltimaCotacaoAcao), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public ActionResult<UltimaCotacaoAcao> GetCotacao(string codigo)
        {
            if (String.IsNullOrWhiteSpace(codigo))
            {
                _logger.LogError(
                    $"GetCotacao - Codigo de Acao nao informado");
                return new BadRequestObjectResult(new
                {
                    Sucesso = false,
                    Mensagem = "Código de Ação não informado"
                });
            }

            _logger.LogInformation($"GetCotacao - codigo da Acao: {codigo}");
            UltimaCotacaoAcao acao = null;
            if (!String.IsNullOrWhiteSpace(codigo))
                acao = _repository.Get(codigo.ToUpper());

            if (acao != null)
            {
                _logger.LogInformation(
                    $"GetCotacao - Acao: {codigo} | Valor atual: {acao.Valor} | Ultima atualizacao: {acao.Data}");
                return new OkObjectResult(acao);
            }
            else
            {
                _logger.LogError(
                    $"GetCotacao - Codigo de Acao nao encontrado: {codigo}");
                return new NotFoundObjectResult(new
                {
                    Sucesso = false,
                    Mensagem = $"Código de Ação não encontrado: {codigo}"
                });
            }
        }

        [HttpGet]
        public ActionResult<List<HistoricoAcao>> GetAll()
        {
            var dados = _repository.GetAll().ToList();
            _logger.LogInformation($"GetAll - encontrado(s) {dados.Count} registro(s)");
            return dados;
        }
    }
}