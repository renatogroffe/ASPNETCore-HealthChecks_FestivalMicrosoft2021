using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.ServiceBus;
using Serilog.Core;
using ProcessadorAcoes.Models;
using ProcessadorAcoes.Validators;
using ProcessadorAcoes.Data;

namespace ProcessadorAcoes
{
    public class ConsoleApp
    {
        private readonly Logger _logger;
        private readonly IConfiguration _configuration;
        private readonly AcoesRepository _repository;
        private static ISubscriptionClient _subscriptionClient;

        public ConsoleApp(Logger logger, IConfiguration configuration,
            AcoesRepository repository)
        {
            _logger = logger;
            _configuration = configuration;
            _repository = repository;
        }

        public void Run()
        {
           _logger.Information("Testando o consumo de mensagens com Azure Service Bus");

            string nomeTopic = _configuration["AzureServiceBus:Topic"];
            string subscription = "consoleapp-redis";
            _subscriptionClient = new SubscriptionClient(
                _configuration["AzureServiceBus:ConnectionString"],
                nomeTopic, subscription);

            _logger.Information($"Topic = {nomeTopic}");
            _logger.Information($"Subscription = {subscription}");

            _logger.Information("Aguardando mensagens...");
            _logger.Information("Pressione Enter para encerrar");
            RegisterOnMessageHandlerAndReceiveMessages();

            Console.ReadLine();
            _subscriptionClient.CloseAsync().Wait();
            _logger.Warning("Encerrando o processamento de mensagens!");            
        }

        private void RegisterOnMessageHandlerAndReceiveMessages()
        {
            var messageHandlerOptions = new MessageHandlerOptions(
                ExceptionReceivedHandler)
            {
                MaxConcurrentCalls = 1,
                AutoComplete = false
            };

            _subscriptionClient.RegisterMessageHandler(
                ProcessMessagesAsync, messageHandlerOptions);
        }

        private async Task ProcessMessagesAsync(Message message, CancellationToken token)
        {
            string dados = Encoding.UTF8.GetString(message.Body);
            _logger.Information($"Mensagem recebida: {dados}");

            var acao = JsonSerializer.Deserialize<Acao>(dados,
                new JsonSerializerOptions()
                {
                    PropertyNameCaseInsensitive = true
                });

            var validationResult = new AcaoValidator().Validate(acao);
            if (validationResult.IsValid)
            {
                _repository.Save(acao);
                _logger.Information("Ação registrada com sucesso!");
            }
            else
            {
                _logger.Error("Dados inválidos para a Ação");
            }

            await _subscriptionClient.CompleteAsync(
                message.SystemProperties.LockToken);
        }

        private Task ExceptionReceivedHandler(ExceptionReceivedEventArgs exceptionReceivedEventArgs)
        {
            _logger.Error($"Message handler - Tratamento - Exception: {exceptionReceivedEventArgs.Exception}.");

            var context = exceptionReceivedEventArgs.ExceptionReceivedContext;
            _logger.Error("Exception context - informaçoes para resolução de problemas:");
            _logger.Error($"- Endpoint: {context.Endpoint}");
            _logger.Error($"- Entity Path: {context.EntityPath}");
            _logger.Error($"- Executing Action: {context.Action}");

            return Task.CompletedTask;
        }
    }
}