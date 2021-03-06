using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Discord.Commands;
using Discord.WebSocket;
using BelfastBot.Services.Configuration;
using BelfastBot.Services.Logging;
using Discord;
using System.Linq;
using BelfastBot.Modules;

#nullable enable

namespace BelfastBot.Services.Commands
{
    public class CommandHandlingService : ICommandHandlingService
    {
        private readonly IServiceProvider m_services;
        private readonly DiscordSocketClient? m_client;
        private readonly CommandService m_command;
        private readonly IBotConfigurationService m_config;
        private readonly LoggingService m_logger;

        public CommandHandlingService(IServiceProvider services)
        {
            m_services = services;
            m_client = services.GetRequiredService<IDiscordClient>() as DiscordSocketClient;
            m_command = services.GetRequiredService<CommandService>();
            m_config = services.GetRequiredService<IBotConfigurationService>();
            m_logger = services.GetRequiredService<LoggingService>();
        }

        public async Task InitializeAsync()
        {
            await m_command.AddModulesAsync(assembly: Assembly.GetAssembly(typeof(CommandHandlingService)),
                                            services: m_services);
        }

        private bool IsIntentionalCommand(string message)
        {
            if(!message.Substring(m_config.Configuration.Prefix.Length).Any(c => char.IsLetter(c)))
                return false;
            return true;
        }

        public async Task HandleCommandAsync(IUserMessage message, bool parsePrefix)
        {
            if (message == null)
            {
                m_logger.LogCritical("Received a message that wasn't a SocketUserMessage");
                return;
            }

            if(!IsIntentionalCommand(message!.Content))
            {
                m_logger.LogInfo("Probably unintentional command, ignoring");
                return;
            }

            int argPos = 0;

            if(parsePrefix)
            {
                if (!(message.HasStringPrefix(m_config.Configuration.Prefix, ref argPos, StringComparison.OrdinalIgnoreCase) ||
                message.HasMentionPrefix(m_client?.CurrentUser, ref argPos)) ||
                message!.Author.IsBot)
                    return;
            }

            BelfastCommandContext context = new BelfastCommandContext(m_client, message);

            Task<IResult> task = m_command.ExecuteAsync(
                context: context,
                argPos: argPos,
                services: m_services);

            IResult? result = null;

            if((await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10)))) == task)
                result = task.Result;

            if(result == null)
            {
                await context.Channel.SendMessageAsync(
                    $"{Emotes.BelfastPout} Sorry For My Misbehaviour Commander！\n" +
                    $"Command timed out"
                );
            }
            else if (!result.IsSuccess)
            {
                await context.Channel.SendMessageAsync(
                    $"{Emotes.BelfastShock} Sorry For My Misbehaviour Commander！\n" +
                    $"{result.ErrorReason}\n" +
                    $"try **{m_config.Configuration.Prefix}help** for lists of commands"
                );
            }
            else
            {
                m_logger.LogInfo("Successfully handled command");
            }
        }
    }
}