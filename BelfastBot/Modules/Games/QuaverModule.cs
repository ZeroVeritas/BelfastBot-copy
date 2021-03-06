using Discord.Commands;
using System.Threading.Tasks;
using QuaverApi;
using Discord;
using BelfastBot.Services.Database;
using BelfastBot.Services.Pagination;
using System.Collections.Generic;
using System;

namespace BelfastBot.Modules.Games
{
    [Summary("Commands for quaver")]
    public class QuaverModule : BelfastModuleBase
    {
        public static Dictionary<IMessageChannel, (DateTime, Map)> LastMapInChannel = new Dictionary<IMessageChannel, (DateTime, Map)>();

        public JsonDatabaseService Db { get; set; }
        public PaginatedMessageService PaginatedMessageService { get; set; }

        private Embed GetUserEmbed((User user, KeyInfo key) data, int index, EmbedFooterBuilder footer) => new EmbedBuilder()
            .WithColor(0x43EBFB)
            .WithAuthor(author => {
                author
                    .WithName($"{data.user.Username}'s Quaver {data.key.KeyCount}K Info")
                    .WithUrl($"https://quavergame.com/profile/{data.user.Id}")
                    .WithIconUrl($"https://quavergame.com/static/flags/4x3/{data.user.Country}.png");
            })
            .AddField("Details ▼", $"" +
            $"__**Main Details**__\n" +
            $"► Accuracy: **{data.key.Stats.Accuracy.ToString("0.00")}**\n" +
            $"► Performance: **{data.key.Stats.PerformanceRating.ToString("0.00")}**\n" +
            $"► Play Count: **{data.key.Stats.PlayCount}**\n" +
            $"__**Ranking**__\n" +
            $"► Global Rank: **{data.key.GlobalRanking}**\n" +
            $"► Country Rank: **{data.key.CountryRanking} [{data.user.Country}]**")
            .WithThumbnailUrl(data.user.AvatarUrl)
            .WithFooter(footer)
            .Build();

        private Embed GetRecentEmbed(User user, Map map, Recent recent)
        {
            (DateTime time, Map map) pair = LastMapInChannel.GetValueOrDefault(Context.Channel);
            if(DateTime.Now > pair.time)
                LastMapInChannel[Context.Channel] = (DateTime.Now, map);
            return new EmbedBuilder()
                .WithColor(0x43EBFB)
                .WithAuthor(author => {
                    author
                        .WithName($"{user.Username}'s Recent 4K Play")
                        .WithUrl($"https://quavergame.com/profile/{user.Id}")
                        .WithIconUrl(user.AvatarUrl);
                })
                .AddField("Details ▼", $"" +
                $"__**Main Details**__\n" +
                $"► Performance: **{recent.PerformanceRating:F2}**\n" +
                $"► Grade: **{GetEmoteForRank(recent.Grade)}**\n" +
                $"► Accuracy: **{recent.Accuracy:F2}**\n" +
                $"► Mods: **{recent.ModsString}**\n" +
                $"► Combo: **{recent.Combo}**\n" +
                $"__**Map**__ {Emotes.Note}\n" +
                $"**[{map.Title}](https://quavergame.com/mapsets/map/{map.Id})**\n" +
                $"► **{map.DifficultyName} [{map.DifficultyRating:F2}☆]**\n" +
                $"► Made By: **[{map.Creator}]**")
                .WithImageUrl($"https://quaver.blob.core.windows.net/banners/{map.MapSetId}_banner.jpg")
                .Build();
        }

        private Embed GetMapEmbed(Map map, int index, EmbedFooterBuilder footer)
        {
            (DateTime time, Map map) pair = LastMapInChannel.GetValueOrDefault(Context.Channel);
            if(DateTime.Now > pair.time)
                LastMapInChannel[Context.Channel] = (DateTime.Now, map);
            return new EmbedBuilder()
                .WithTitle($"{map.Title}")
                .AddField("Created by ", $"{map.Creator}")
                .WithFooter(footer)
                .Build();
        }

        private IEmote GetEmoteForRank(string rank)
        {
            switch (rank)
            {
                case "X":
                    return Emotes.X;
                case "SS":
                    return Emotes.SS;
                case "S":
                    return Emotes.S;
                case "A":
                    return Emotes.A;
                case "B":
                    return Emotes.B;
                case "C":
                    return Emotes.C;
                case "D":
                    return Emotes.D;
                case "F":
                    return Emotes.F;
            }
            return Emotes.BelfastShock;
        }

        [Command("quaver")]
        [RateLimit(typeof(QuaverModule), perMinute: 45)]
        [Summary("Get info about user")]
        public async Task GetUserAsync([Summary("Name to get info about")] [Remainder] string name = null)
        {
            uint? id = await TryGetUserData(name, user => NormalDatabaseAccessor(user, entry => entry.QuaverId), name => Client.GetUserIdByNameAsync(name).Result);

            if (id == null)
            {
                await ReplyAsync("> Couldn't find any user, please set one or specify in arguments");
                return;
            }

            uint userId = id.Value;

            User user = await Client.GetUserAsync(userId);
            (User, KeyInfo)[] keys = { (user, user.FourKeys), (user, user.SevenKeys) };

            await PaginatedMessageService.SendPaginatedDataMessageAsync(Context.Channel, keys, GetUserEmbed);
        }

        [Command("qrecent4k"), Alias("qr4k")]
        [RateLimit(typeof(QuaverModule), perMinute: 45)]
        [Summary("Get recent 4k play info by user")]
        public async Task GetRecent4KAsync([Summary("User to get recent play from")] string name = null)
        {
            uint? id = null;
            if (string.IsNullOrEmpty(name))
                id = Db.GetUserEntry(0, Context.User.Id).QuaverId;
            else
                id = await Client.GetUserIdByNameAsync(name);

            if (id == null)
            {
                await ReplyAsync("> Couldn't find any user, please set one or specify in arguments");
                return;
            }

            uint userId = id.Value;

            Recent recent = await Client.GetUserRecentAsync(userId, 1);
            Map map = recent.Map;
            User user = await Client.GetUserAsync(userId);

            await ReplyAsync(embed: GetRecentEmbed(user, map, recent));
        }

        [Command("qrecent7k"), Alias("qr7k")]
        [RateLimit(typeof(QuaverModule), perMinute: 45)]
        [Summary("Get recent 7k play info by user")]
        public async Task GetRecent7KAsync([Summary("User to get recent play from")] string name = null)
        {
            uint? id = null;
            if (string.IsNullOrEmpty(name))
                id = Db.GetUserEntry(0, Context.User.Id).QuaverId;
            else
                id = await Client.GetUserIdByNameAsync(name);

            if (id == null)
            {
                await ReplyAsync("> Couldn't find any user, please set one or specify in arguments");
                return;
            }

            uint userId = id.Value;

            Recent recent = await Client.GetUserRecentAsync(userId, 2);
            Map map = recent.Map;
            User user = await Client.GetUserAsync(userId);

            await ReplyAsync(embed: GetRecentEmbed(user, map, recent));
        }

        [Command("qmap")]
        [RateLimit(typeof(QuaverModule), perMinute: 45)]
        [Summary("Gives information about a map")]
        public async Task MapAsync(string mapName)
        {
            if(uint.TryParse(mapName, out uint id))
            {
                Map map = await Client.GetMapAsync(id);
                if(map == null)
                {
                    await ReplyAsync("No such map");
                    return;
                }
                await ReplyAsync(embed: GetMapEmbed(map, 0, new EmbedFooterBuilder()));
            }
            else
            {
                await ReplyAsync("Invalid map id provided");
            }
        }

        [Command("qmap")]
        [RateLimit(typeof(QuaverModule), perMinute: 45)]
        [Summary("Gives information about a map that was just sent")]
        public async Task MapAsync()
        {
            if(LastMapInChannel.TryGetValue(Context.Channel, out (DateTime time, Map map) pair))
            {
                if(pair.map == null)
                {
                    await ReplyAsync("Map is null");
                    return;
                }
                await ReplyAsync(embed: GetMapEmbed(pair.map, 0, new EmbedFooterBuilder()));
            }
            else
            {
                await ReplyAsync("No map found in this channel");
            }
        }
    }
}