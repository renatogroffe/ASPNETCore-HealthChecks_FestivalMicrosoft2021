using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using HealthChecks.UI.Client;

namespace SiteMonitoramento
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            // Configurando a verificação de disponibilidade de diferentes
            // serviços através de Health Checks
            services.AddHealthChecks()
                .AddAzureServiceBusTopic(Configuration.GetConnectionString("AzureServiceBusTopicAcoes"),
                    topicName: "topic-acoes",
                    name: "azureservicebus_topic-acoes", tags: new string[] { "messaging" })
                .AddSqlServer(Configuration.GetConnectionString("SqlServer"),
                    name: "sqlserver", tags: new string[] { "db", "data" })
                .AddRedis(Configuration.GetConnectionString("Redis"),
                    name: "redis", tags: new string[] { "db", "data", "nosql" })
                .AddUrlGroup(new System.Uri(Configuration["UrlAPIAcoes"]),
                    name: "apiacoes", tags: new string[] { "url", "rest", "api", "webapp" });

            services.AddHealthChecksUI()
                .AddInMemoryStorage();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // Gera o endpoint que retornará os dados utilizados no dashboard
            app.UseHealthChecks("/healthchecks-data-ui", new HealthCheckOptions()
            {
                Predicate = _ => true,
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });

            // Ativa o dashboard para a visualização da situação de cada Health Check
            app.UseHealthChecksUI(options =>
            {
                options.UIPath = "/monitor";
            });
        }
    }
}