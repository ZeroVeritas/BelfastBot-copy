using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Discord;
using BelfastBot;
using BelfastBot.Services.Database;
using BelfastBot.Services.Logging;
using BelfastBot.Services.Scheduler;
using BelfastBot.Services.Pagination;
using BelfastBot.Services.Credits;
using BelfastBot.Services.Giveaway;
using System.Net.Http;
using BelfastBot.Services.Commands;
using Discord.Commands;
using BelfastBot.Services.Configuration;
using BelfastBot.Services.Communiciation;
using Moq;

namespace BelfastWebClient
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRazorPages();
            services.AddSingleton<IDiscordClient>(coll => new Mock<IDiscordClient>().Object)
                .AddSingleton<IBotConfigurationService, BotConfigurationService>()
                .AddSingleton<HttpClient>()
                .AddSingleton<WebBelfastClient>()
                    .AddSingleton<IClient>(coll => coll.GetRequiredService<WebBelfastClient>())
                .AddSingleton<WebCommunicationService>()
                .AddSingleton<ICommunicationService>(coll => coll.GetRequiredService<WebCommunicationService>())
                .AddSingleton<JsonDatabaseService>()
                .AddSingleton<LoggingService>()
                .AddSingleton<SchedulerService>()
                .AddSingleton<PaginatedMessageService>()
                .AddSingleton<MessageRewardService>()
                .AddSingleton<GiveawayService>()
                .AddSingleton<DummyType>()
                .AddSingleton<CommandService>()
                .AddSingleton<WebCommandHandlingService>()
                .AddSingleton<IServiceProvider>(coll => coll);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();
                endpoints.MapControllerRoute("api", "{controller}/{action}");
            });

            app.ApplicationServices.GetRequiredService<WebCommandHandlingService>().Initialize();
        }
    }
}
