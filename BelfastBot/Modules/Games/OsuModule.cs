using Discord.Commands;
using System.Threading.Tasks;
using OsuApi;
using Discord;
using System.Linq;
using BelfastBot.Services.Database;
using BelfastBot.Services.Pagination;
using Common;
using System.Collections.Generic;
using System;

namespace BelfastBot.Modules.Games
{
    [Summary("Commands for osu")]
    public class OsuModule : BelfastModuleBase
    {
        public static Dictionary<IMessageChannel, (DateTime, Beatmap)> LastBeatmapInChannel = new Dictionary<IMessageChannel, (DateTime, Beatmap)>();

        public JsonDatabaseService Db { get; set; }
        public PaginatedMessageService PaginatedMessageService { get; set; }
        public IDiscordClient IClient { get; set; }

        public int GetIndexFromModeName(string name, int defaultValue = -1) => name.ToLower() switch
        {
            "std" => 0,
            "taiko" => 1,
            "ctb" => 2,
            "mania" => 3,
            _ => defaultValue,
        };

        private string GetNameForModeIndex(uint mode)
        {
            switch (mode)
            {
                case 0:
                    return "standard";
                case 1:
                    return "taiko";
                case 2:
                    return "ctb";
                case 3:
                    return "mania";
            }
            return "Unknown";
        }

        private string GetLinkSuffixForModeIndex(uint mode)
        {
            switch (mode)
            {
                case 0:
                    return "osu";
                case 1:
                    return "taiko";
                case 2:
                    return "fruits";
                case 3:
                    return "mania";
            }
            return "Unknown";
        }

        private IEmote GetEmoteForRank(string rank) => rank switch
        {
            "X" => Emotes.SS,
            "XH" => Emotes.XH,
            "S" => Emotes.S,
            "SH" => Emotes.SH,
            "A" => Emotes.A,
            "B" => Emotes.B,
            "C" => Emotes.C,
            "D" => Emotes.D,
            "F" => Emotes.F,
            _ => Emotes.BelfastShock,
        };

        private Embed GetUserProfileEmbed(UserProfile user, int index, EmbedFooterBuilder footer) => new EmbedBuilder()
            .WithColor(0xE664A0)
            .WithAuthor(author => {
                author
                    .WithName($"{user.UserName}'s osu!{GetNameForModeIndex(user.Mode)} Data")
                    .WithUrl($"https://osu.ppy.sh/users/{user.UserId}/{GetLinkSuffixForModeIndex(user.Mode)}")
                    .WithIconUrl($"https://osu.ppy.sh/images/flags/{user.Country}.png");
            })
            .AddField("Details ▼", $"" +
            $"__**Main Details**__\n" +
            $"► Accuracy: **{user.Accuracy:F2}%**\n" +
            $"► PP: **{user.PP:F2}**\n" +
            $"► Play Count: **{user.PlayCount}**\n" +
            $"► Level: **{user.Level:F0}**\n" +
            $"__**Ranking**__\n" +
            $"► Global Rank: **{user.GlobalRanking}**\n" +
            $"► Country Rank: **{user.CountryRanking} [{user.Country}]**")
            .WithThumbnailUrl($"https://a.ppy.sh/{user.UserId}")
            .WithFooter(footer)
            .Build();

        private Embed GetBeatmapResultEmbed(PlayResult result, int index, EmbedFooterBuilder footer)
        { 
            (DateTime time, Beatmap beatmap) pair = LastBeatmapInChannel.GetValueOrDefault(Context.Channel);
            if(DateTime.Now > pair.time)
                LastBeatmapInChannel[Context.Channel] = (DateTime.Now, result.BeatmapData);
            return new EmbedBuilder()
                .WithColor(0xE664A0)
                .WithAuthor(author => {
                    author
                        .WithName($"{result.PlayerData.UserName}'s Recent osu!{GetNameForModeIndex(result.PlayerData.Mode)} Play")
                        .WithUrl($"https://osu.ppy.sh/users/{result.PlayerData.UserId}/{GetLinkSuffixForModeIndex(result.PlayerData.Mode)}")
                        .WithIconUrl($"https://a.ppy.sh/{result.PlayerData.UserId}");
                })
                .AddField($"Details ▼", $"" +
                $"__**Main Details**__\n" +
                $"► Rank: **{GetEmoteForRank(result.Rank)}**\n" +
                $"► Accuracy: **{(result.Accuracy?.Accuracy ?? 0) * 100:F2}%**\n" +
                $"► PP: **{result.PP:F2} ({result.SSPP:F2} if SS)**\n" +
                $"► Mods: **{"None".IfTargetIsNullOrEmpty(result.Mods.ToShortString())}**\n" +
                $"► Score: **{result.Score}**\n" +
                $"► Combo: **{result.Combo}**\n" +
                $"__**Beatmap**__ {Emotes.Note}\n" +
                $"**[{result.BeatmapData.Name}](https://osu.ppy.sh/b/{result.BeatmapData.Id})**\n" +
                $"► **[{result.BeatmapData.StarRating:F2}☆] {result.BeatmapData.Bpm}** Bpm\n" +
                $"► Lenght **{result.BeatmapData.Length.ToShortForm()}**\n" +
                $"► Made By: **[{result.BeatmapData.CreatorName}](https://osu.ppy.sh/users/{result.BeatmapData.CreatorId})**")
                .WithImageUrl($"https://assets.ppy.sh/beatmaps/{result.BeatmapData.SetId}/covers/cover.jpg")
                .WithFooter(footer)
                .Build();
        }

        private Embed GetBeatmapEmbed(Beatmap beatmap, int index, EmbedFooterBuilder footer)
        {
            (DateTime time, Beatmap beatmap) pair = LastBeatmapInChannel.GetValueOrDefault(Context.Channel);
            if(DateTime.Now > pair.time)
                LastBeatmapInChannel[Context.Channel] = (DateTime.Now, beatmap);
            return new EmbedBuilder()
                .WithTitle($"{beatmap.Name}")
                .AddField("Created by ", $"{beatmap.CreatorName}")
                .WithFooter(footer)
                .Build();
        }

        [Command("osu")]
        [RateLimit(typeof(OsuModule), perMinute: 45)]
        [Summary("Get std profile details from an user")]
        public async Task OsuGetUserAsync([Summary("Name to search")] [Remainder] string target_name = "")
        {
            string username = await TryGetUserData(target_name, user =>　NotNullOrEmptyStringDatabaseAccessor(user, entry => entry.OsuName));

            if (username == null)
            {
                await ReplyAsync("> No user **{username}** found");
                return;
            }

            Logger.LogInfo($"Searching for user {username} on Osu");

            const int modeCount = 4;
            var taskList = Enumerable.Range(0, modeCount).Select(i => Client.GetUserAsync(Config.Configuration.OsuApiToken, username, (uint)i));
            UserProfile[] results = await Task.WhenAll(taskList);

            results = results.Where(res => res != null).ToArray();

            if(results.Length <= 0)
                await ReplyAsync($"> No user **{username}** found");
            else
                await PaginatedMessageService.SendPaginatedDataMessageAsync(Context.Channel, results, GetUserProfileEmbed);
        }

        [Command("orecent"), Alias("ors")]
        [RateLimit(typeof(OsuModule), perMinute: 45)]
        [Summary("Get recent play")]
        public async Task GetRecentPlay(string target_mode = "_", [Summary("Name to search")] [Remainder] string target_name = "")
        {
            string username = await TryGetUserData(target_name, user =>　NotNullOrEmptyStringDatabaseAccessor(user, entry => entry.OsuName));

            if (username == null)
            {
                await ReplyAsync("> No user **{username}** found");
                return;
            }

            Logger.LogInfo($"Searching for user {username}'s recent plays on Osu");

            int mode = GetIndexFromModeName(target_mode);

            if(mode == -1)
            {
                const int modeCount = 4;
                Task<PlayResult[]>[] taskList = new Task<PlayResult[]>[modeCount];
                for (uint i = 0; i < modeCount; i++)
                    taskList[i] = Client.GetUserRecentAsync(Config.Configuration.OsuApiToken, username, i, 1);
                PlayResult[][] results = await Task.WhenAll(taskList);

                PlayResult[][] validResults = results.Select(a => a.Where(result => (result?.BeatmapData?.Id ?? 0) != 0).ToArray()).ToArray();

                if (validResults.Length > 0)
                    await PaginatedMessageService.SendPaginatedDataMessageAsync(Context.Channel, validResults.Select(a => a.Length > 0 ? a[0] : null).ToArray(), GetBeatmapResultEmbed);
                else
                    await ReplyAsync($"> No best plays found for {username}");
            }
            else
            {
                PlayResult[] results = await Client.GetUserRecentAsync(Config.Configuration.OsuApiToken, username, (uint)mode);
                if(results.Length > 0)
                    await PaginatedMessageService.SendPaginatedDataMessageAsync(Context.Channel, results, GetBeatmapResultEmbed);
                else
                    await ReplyAsync($"> No best plays found for {username}");
            }
        }

        [Command("obest"), Alias("obs")]
        [RateLimit(typeof(OsuModule), perMinute: 45)]
        [Summary("Get recent play")]
        public async Task GetBestPlays(string target_mode = "_", [Summary("Name to search")] string target_name = "")
        {
            string username = await TryGetUserData(target_name, user =>　NotNullOrEmptyStringDatabaseAccessor(user, entry => entry.OsuName));

            if (username == null)
            {
                await ReplyAsync("> No user **{username}** found");
                return;
            }

            Logger.LogInfo($"Searching for user {username}'s best plays on Osu");

            int mode = GetIndexFromModeName(target_mode);

            if(mode == -1)
            {
                const int modeCount = 4;
                Task<PlayResult[]>[] taskList = new Task<PlayResult[]>[modeCount];
                for (uint i = 0; i < modeCount; i++)
                    taskList[i] = Client.GetUserBestAsync(Config.Configuration.OsuApiToken, username, i, 1);
                PlayResult[][] results = await Task.WhenAll(taskList);

                PlayResult[][] validResults = results.Select(a => a.Where(result => (result?.BeatmapData?.Id ?? 0) != 0).ToArray()).ToArray();

                if (validResults.Length > 0)
                    await PaginatedMessageService.SendPaginatedDataMessageAsync(Context.Channel, validResults.Select(a => a.Length > 0 ? a[0] : null)?.ToArray(), GetBeatmapResultEmbed);
                else
                    await ReplyAsync($"> No best plays found for {username}");
            }
            else
            {
                PlayResult[] results = await Client.GetUserBestAsync(Config.Configuration.OsuApiToken, username, (uint)mode);
                if(results.Length > 0)
                    await PaginatedMessageService.SendPaginatedDataMessageAsync(Context.Channel, results, GetBeatmapResultEmbed);
                else
                    await ReplyAsync($"> No best plays found for {username}");
            }          
        }

        [Command("omap")]
        [RateLimit(typeof(QuaverModule), perMinute: 45)]
        [Summary("Gives information about a map")]
        public async Task MapAsync(string map, string mode = "std")
        {
            if(ulong.TryParse(map, out ulong id))
            {
                Beatmap beatmap = await Client.GetBeatmapAsync(Config.Configuration.OsuApiToken, id, (uint)GetIndexFromModeName(mode, 0));
                if(beatmap == null)
                {
                    await ReplyAsync("No such map");
                    return;
                }
                await ReplyAsync(embed: GetBeatmapEmbed(beatmap, 0, new EmbedFooterBuilder()));
            }
            else
            {
                await ReplyAsync("Invalid map id provided");
            }
        }

        [Command("omap")]
        [RateLimit(typeof(QuaverModule), perMinute: 45)]
        [Summary("Gives information about a map that was just sent")]
        public async Task MapAsync()
        {
            if(LastBeatmapInChannel.TryGetValue(Context.Channel, out (DateTime time, Beatmap beatmap) pair))
            {
                if(pair.beatmap == null)
                {
                    await ReplyAsync("Map is null");
                    return;
                }
                await ReplyAsync(embed: GetBeatmapEmbed(pair.beatmap, 0, new EmbedFooterBuilder()));
            }
            else
            {
                await ReplyAsync("No map found in this channel");
            }
        }
    }
}