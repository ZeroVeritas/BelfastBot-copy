using Common;
using Discord;
using Discord.Commands;
using BelfastBot.Services.Pagination;
using System.Linq;
using System.Threading.Tasks;
using AnimeApi;
using BelfastBot.Services.Database;
using System.Net;
using System;
using TraceMoeApi;
using System.Collections.Generic;

namespace BelfastBot.Modules.Otaku
{
    [Summary("Commands for Japan related stuff")]
    public class OtakuModule : BelfastModuleBase
    {
        public PaginatedMessageService PaginatedMessageService { get; set; }
        public JsonDatabaseService Db { get; set; }

        #region Commands

        [Command("trace")]
        [RateLimit(typeof(OtakuModule), perMinute: 45)]
        [Summary("Trace an image to find source of it\n" +
            "Please Refain From Using Discord Image Links")]
        public async Task TraceImageAsync([Summary("Image to search")] [Remainder]string url = null)
        {
            Logger.LogInfo($"Tracing image {url}");

            AnimeResult animeResult = new AnimeResult();

            if (url == null)
            {
                if (Context.Message.Attachments.Count == 1)
                {
                    IAttachment attachment = Context.Message.Attachments.ElementAt(0);
                    using (WebClient wc = new WebClient())
                    {
                        byte[] data = wc.DownloadData(attachment.Url);
                        string base64Data = Convert.ToBase64String(data);
                        TraceResult traceResult = (await Client.GetTraceResultsFromBase64Async(base64Data, 1))[0];
                        animeResult = await AnilistClient.GetAnimeAsync(traceResult.AlId);
                    }
                }
                else
                {
                    var messagesEnumerable = Context.Channel.GetMessagesAsync(2);
                    IEnumerable<IMessage> messages  = await messagesEnumerable.FlattenAsync();
                    if(messages.Count() < 2)
                    {
                        await ReplyAsync($"Too Few Messages");
                        return;
                    }
                    IMessage message = messages.ElementAt(1);

                    if(message.Attachments.Count < 1)
                    {
                        await ReplyAsync("No Images Were Found On Previous Message");
                        return;
                    }
                    IAttachment attachment = message.Attachments.ElementAt(0);
                    using (WebClient wc = new WebClient())
                    {
                        byte[] data = wc.DownloadData(attachment.Url);
                        string base64Data = Convert.ToBase64String(data);
                        TraceResult traceResult = (await Client.GetTraceResultsFromBase64Async(base64Data, 1))[0];
                        animeResult = await AnilistClient.GetAnimeAsync(traceResult.AlId);
                    }
                }
            }
            else
            {
                if (url.Contains("cdn.discordapp.com"))
                {
                    await ReplyAsync("Discord image links are not supported, please send the image directly as an attachement");
                    return;
                }

                TraceResult traceResult = (await Client.GetTraceResultsAsync(url, 1))[0];
                animeResult = await AnilistClient.GetAnimeAsync(traceResult.AlId);
            }

            await ReplyAsync(embed: GetAnimeResultEmbed(animeResult, 0, new EmbedFooterBuilder()));
        }


        [Command("malanime"), Alias("mala")]
        [RateLimit(typeof(OtakuModule), perMinute: 45)]
        [Summary("Search for anime on myanimelist")]
        public async Task SearchMalAnimeAsync([Summary("Title to search")] [Remainder]string name = "Azur Lane")
        {
            Logger.LogInfo($"Searching for {name} on myanimelist");

            ulong[] ids = await MalClient.GetAnimeIdAsync(name);
            AnimeResult[] resultCache = new AnimeResult[ids.Length];

            await PaginatedMessageService.SendPaginatedDataAsyncMessageAsync(Context.Channel, ids, async (ulong id, int index, EmbedFooterBuilder footer) => {
                if (resultCache[index].MalId != 0)
                    return GetAnimeResultEmbed(resultCache[index], index, footer);
                else
                {
                    AnimeResult result = resultCache[index] = await MalClient.GetDetailedAnimeResultsAsync(id);
                    return GetAnimeResultEmbed(result, index, footer);
                }
            });
        }

        [Command("malmanga"), Alias("malm")]
        [RateLimit(typeof(OtakuModule), perMinute: 45)]
        public async Task SearchMalMangaAsync([Summary("Title to search")] [Remainder]string name = "Azur Lane")
        {
            Logger.LogInfo($"Searching for {name} on myanimelist");

            ulong[] ids = await MalClient.GetMangaIdAsync(name);
            MangaResult[] resultCache = new MangaResult[ids.Length];

            await PaginatedMessageService.SendPaginatedDataAsyncMessageAsync(Context.Channel, ids, async (ulong id, int index, EmbedFooterBuilder footer) => {
                if (resultCache[index].MalId != 0)
                    return GetMangaResultEmbed(resultCache[index], index, footer);
                else
                {
                    MangaResult result = resultCache[index] = await MalClient.GetDetailedMangaResultsAsync(id);
                    return GetMangaResultEmbed(result, index, footer);
                }
            });
        }

        [Command("alanime"), Alias("ala")]
        [RateLimit(typeof(OtakuModule), perMinute: 45)]
        [Summary("Search for anime on anilist")]
        public async Task SearchAlAnimeAsync([Summary("Title to search")] [Remainder]string name = "Azur Lane")
        {
            Logger.LogInfo($"Searching for {name} on anilist");

            AnimeResult animeResult = await AnilistClient.GetAnimeAsync(name);

            await ReplyAsync(embed: GetAnimeResultEmbed(animeResult, 0, new EmbedFooterBuilder()));
        }

        [Command("alamanga"), Alias("alm")]
        [RateLimit(typeof(OtakuModule), perMinute: 45)]
        [Summary("Search for manga on anilist")]
        public async Task SearchAlMangaAsync([Summary("Title to search")] [Remainder]string name = "Azur Lane")
        {
            Logger.LogInfo($"Searching for {name} on anilist");

            MangaResult mangaResult = await AnilistClient.GetMangaAsync(name);

            await ReplyAsync(embed: GetMangaResultEmbed(mangaResult, 0, new EmbedFooterBuilder()));
        }

        [Command("aluser"), Alias("alu")]
        [RateLimit(typeof(OtakuModule), perMinute: 45)]
        [Summary("Search for a User on anilist")]
        public async Task SearchAlUserAsync([Summary("Title to search")] [Remainder]string target_name = null)
        {
            string username = await TryGetUserData(target_name, user =>　NotNullOrEmptyStringDatabaseAccessor(user, entry => entry.AnilistName));

            if (username == null)
            {
                await ReplyAsync("> No user **{username}** found");
                return;
            }

            Logger.LogInfo($"Searching for {username} on anilist");

            UserResult? userResult = await AnilistClient.GetUserAsync(username);

            if(userResult == null)
                await ReplyAsync($"> No user **{username}** found");
            else
                await ReplyAsync(embed: GetUserResultEmbed(userResult.Value, 0, new EmbedFooterBuilder()));
        }
        #endregion

        #region Embed
        private Embed GetUserResultEmbed(UserResult result, int index, EmbedFooterBuilder footer) => new EmbedBuilder()
            .WithColor(0x2E51A2)
            .WithAuthor(author => {
                author
                    .WithName($"Anilist Data of {result.Name}")
                    .WithUrl($"{result.SiteUrl}")
                    .WithIconUrl(result.ApiType.ToIconUrl());
            })
            .AddField("Statistics ▼",
            $"__**Anime Stats:**__\n" +
            $"► Total Count: **{result.AnimeStats.Count}**\n" +
            $"► Episodes Watched: **{result.AnimeStats.Amount}**\n" +
            $"► Mean Score: **{result.AnimeStats.MeanScore}**\n" +
            $"__**Manga Stats:**__\n" +
            $"► Total Count: **{result.MangaStats.Count}**\n" +
            $"► Chapters Read: **{result.MangaStats.Amount}**\n" +
            $"► Mean Score: **{result.MangaStats.MeanScore}**\n")
            .AddField("Favorites ▼",
            $"► Anime: **[{result.AnimeFavorite?.Name?.ShortenText(limit: 25) ?? "None"}]({result.AnimeFavorite?.SiteUrl})**\n" +
            $"► Manga: **[{result.MangaFavorite?.Name?.ShortenText(limit: 25) ?? "None"}]({result.MangaFavorite?.SiteUrl})**\n" +
            $"► Character: **[{result.CharacterFavorite?.Name?.ShortenText(limit: 25) ?? "None"}]({result.CharacterFavorite?.SiteUrl})**\n")
            .WithFooter(footer)
            .WithThumbnailUrl(result.AvatarImage)
            .WithImageUrl(result.BannerImage)
            .Build();

        private Embed GetMangaResultEmbed(MangaResult result, int index, EmbedFooterBuilder footer) => new EmbedBuilder()
            .WithColor(0x2E51A2)
            .WithAuthor(author => {
                author
                    .WithName($"{result.Title}")
                    .WithUrl($"{result.SiteUrl}")
                    .WithIconUrl(result.ApiType.ToIconUrl());
            })
            .WithDescription($"" +
            $"__**Description:**__\n" +
            $"{result.Synopsis.ShortenText()}")
            .AddField("Details ▼",
            $"► Type: **{result.Type}**\n" +
            $"► Status: **{result.Status}**\n" +
            $"► Chapters: **{"Unknown".IfTargetIsNullOrEmpty(result.Chapters?.ToString())}**\n" +
            $"► Volumes: **{"Unknown".IfTargetIsNullOrEmpty(result.Volumes?.ToString())}**\n" +
            $"► Average Score: **{"NaN".IfTargetIsNullOrEmpty(result.Score?.ToString())}**☆\n" +
            $"► Author(s): **{(result.Staff.Length > 0 ? result.Staff.Select(author => $"[{author.Name}]({author.SiteUrl})").CommaSeperatedString() : "Unknown")}**\n")
            .WithFooter(footer)
            .WithImageUrl(result.ImageUrl)
            .Build();

        private Embed GetAnimeResultEmbed(AnimeResult result, int index, EmbedFooterBuilder footer) => new EmbedBuilder()
            .WithColor(0x2E51A2)
            .WithAuthor(author => {
                author
                    .WithName($"{result.Title}")
                    .WithUrl($"{result.SiteUrl}")
                    .WithIconUrl(result.ApiType.ToIconUrl());
            })
            .WithDescription($"" +
            $"__**Description:**__\n" +
            $"{result.Synopsis.ShortenText()}")
            .AddField("Details ▼", 
            $"► Type: **{result.Type}** [Source: **{result.Source}**] \n" +
            $"► Status: **{result.Status}**\n" +
            $"► Episodes: **{"Unknown".IfTargetIsNullOrEmpty(result.Episodes?.ToString())} [{result.Duration} Min]** \n" +
            $"► Score: **{"NaN".IfTargetIsNullOrEmpty($"{result.Score?.ToString()}")}**☆\n" +
            $"► Studio: **[{"Unknown".IfTargetIsNullOrEmpty(result.Studio?.ToString())}]({result.StudioUrl})**\n" +
            $"Broadcast Time: **[{"Unknown".IfTargetIsNullOrEmpty(result.Broadcast?.ToString())}]**\n" +
            $"**{(result.TrailerUrl != null ? $"[Trailer]({result.TrailerUrl})" : "No trailer")}**\n")
            .WithFooter(footer)
            .WithImageUrl(result.ImageUrl)
            .Build();
        #endregion
    }
}