using System;
using System.Linq;
using System.Net.Mime;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.EntityFrameworkCore;
using FluentValidation;
using FluentValidation.AspNetCore;
using StackExchange.Redis;
using APIAcoes.Models;
using APIAcoes.Validators;
using APIAcoes.Data;

namespace APIAcoes
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
            services.AddEntityFrameworkSqlServer()
                .AddDbContext<AcoesContext>(
                    options => options.UseSqlServer(
                        Configuration.GetConnectionString("BaseAcoesEF")));

            services.AddSingleton<ConnectionMultiplexer>(
                ConnectionMultiplexer.Connect(
                    Configuration["Redis:Connection"]));

            services.AddControllers()
                .AddFluentValidation();

            services.AddTransient<IValidator<Acao>, AcaoValidator>();

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "APIAcoes", Version = "v1" });
            });

            services.AddApplicationInsightsTelemetry(Configuration);
            services.ConfigureTelemetryModule<DependencyTrackingTelemetryModule>(
                (module, o) =>
                {
                    module.EnableSqlCommandTextInstrumentation = true;
                });

            services.AddAzureAppConfiguration();

            services.AddScoped<AcoesRepository>();

            services.AddHealthChecks()
                .AddAzureServiceBusTopic(Configuration["AzureServiceBus:ConnectionString"],
                    topicName: "topic-acoes",
                    name: "azureservicebus_topic-acoes", tags: new string[] { "messaging" })
                .AddSqlServer(Configuration.GetConnectionString("BaseAcoes"),
                    name: "sqlserver", tags: new string[] { "db", "data" })
                .AddRedis(Configuration["Redis:Connection"],
                    name: "redis", tags: new string[] { "db", "data", "nosql" });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "APIAcoes v1");
                c.RoutePrefix = string.Empty;
            });

            app.UseCors(builder => builder.AllowAnyMethod()
                                          .AllowAnyOrigin()
                                          .AllowAnyHeader());

            app.UseAzureAppConfiguration();

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseHealthChecks("/status");

            app.UseHealthChecks("/status-detailed",
               new HealthCheckOptions()
               {
                   ResponseWriter = async (context, report) =>
                   {
                       var result = JsonSerializer.Serialize(
                           new
                           {
                               statusApplication = report.Status.ToString(),
                               healthChecks = report.Entries.Select(e => new
                               {
                                   check = e.Key,
                                   status = Enum.GetName(typeof(HealthStatus), e.Value.Status)
                               })
                           });
                       context.Response.ContentType = MediaTypeNames.Application.Json;
                       await context.Response.WriteAsync(result);
                   }
               });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}