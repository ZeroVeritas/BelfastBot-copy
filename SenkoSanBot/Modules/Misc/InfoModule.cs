﻿using Discord;
using Discord.Commands;
using System.Linq;
using System.Threading.Tasks;

namespace SenkoSanBot.Modules.Misc
{
    [Summary("Commands for information")]
    public class InfoModule : SenkoSanModuleBase
    {
        public CommandService Command { get; set; }

        [Command("help")]
        public async Task HelpAsync([Remainder] string command = null)
        {
            Logger.LogInfo($"{Context.User} asked for help about {command ?? "all commands"}");

            if (command == null)
                await ModulesHelp();
            else
                await CommandHelp(command);
        }

        private async Task ModulesHelp()
        {
            EmbedBuilder builder = new EmbedBuilder();

            builder.Description = $"Do {Prefix}help [command] to get more information about a command";
            builder.WithThumbnailUrl(Emotes.SenkoThink.Url);
            builder.WithColor(0xffae0d);

            int i = 0;

            foreach (ModuleInfo module in Command.Modules.GroupBy(x => x.Name).Select(y => y.First()))
            {
                i++;

                string[] commandNames = module.Commands.Select(cmd => cmd.Name).ToArray();

                if (commandNames.Length > 0)
                {
                    builder.AddField($"{i}", $"" +
                        $"__**{module.Name.Replace("Module", " ")} - {module.Summary ?? ""}**__\n" +
                        $"{commandNames.CommaSeperatedString()}");
                }
            }

            await ReplyAsync(embed: builder.Build());
        }

        private async Task CommandHelp(string command)
        {
            SearchResult result = Command.Search(Context, command);
            if (!result.IsSuccess || result.Commands.Count == 0)
            {
                await ReplyAsync($"Couldn't find command '{command}'");
                return;
            }

            foreach (CommandInfo commandInfo in result.Commands.Select(cmd => cmd.Command))
            {
                EmbedBuilder builder = new EmbedBuilder();

                builder.Title = $"{commandInfo.Name} {(commandInfo.Aliases.Count > 1 ? $"({commandInfo.Aliases[1]})" : "")} - {commandInfo.Summary ?? "No information about the command specified"}";
                builder.WithColor(0xffae0d);

                foreach (ParameterInfo parameter in commandInfo.Parameters)
                {
                    string name;
                    if (parameter.IsOptional) name = $"({parameter.Name}) defaults to \"{parameter.DefaultValue}\"";
                    else name = $"[{parameter.Name}]";
                    builder.AddField(name, parameter.Summary ?? "No information specified");
                }

                await ReplyAsync(embed: builder.Build());
            }
        }

        [Command("about")]
        public async Task About()
        {
            Logger.LogInfo($"{Context.User} requested about page");

            Embed embed = new EmbedBuilder()
                .WithColor(0x308ED6)
                .AddField("About ▼", $"" +
                $"► Made by: **Team Shinaosu**\n" +
                $"► Version: **{SenkoSan.Version}**\n" +
                $"► Developers: **[Nobbele](https://github.com/nobbele) & [TheJayDuck](https://github.com/thejayduck)**")
                .WithFooter("09/07-2019")
                .WithThumbnailUrl("https://cdn.discordapp.com/attachments/303528930634235904/629383238917292042/29692031.png")
                .Build();

            await ReplyAsync(embed: embed);
        }
    }
}