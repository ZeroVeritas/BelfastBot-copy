using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using BelfastBot.Services.Communiciation;
using BelfastBot.Services.Logging;

namespace BelfastBot.Services.Pagination
{
    public class PaginatedMessageService
    {
        public delegate Task ReactionCallback(IUserMessage message, PaginationMove move);

        public static readonly int CallbackBufferSize = 16;

        public readonly IEmote First = new Emoji("⏮");
        public readonly IEmote Previous = new Emoji("◀");
        public readonly IEmote Next = new Emoji("▶");
        public readonly IEmote Last = new Emoji("⏭");

        public IEmote[] ReactionEmotes => new IEmote[]
        {
            First, Previous, Next, Last
        };

        private Buffer<(ulong Id, ReactionCallback Callback)> m_callbacks = new Buffer<(ulong, ReactionCallback)>(16);

        private readonly IServiceProvider m_services;
        public BaseSocketClient m_client;
        public LoggingService m_logger;
        public ICommunicationService m_communication;

        public PaginatedMessageService(IServiceProvider services)
        {
            m_services = services;
            m_client = services.GetRequiredService<IDiscordClient>() as BaseSocketClient;
            m_logger = services.GetRequiredService<LoggingService>();
            m_communication = services.GetRequiredService<ICommunicationService>();
        }

        public async Task InitializeAsync()
        {
            if(m_client == null)
            {
                m_logger.LogWarning($"[{nameof(PaginatedMessageService)}] m_client is null, ignoring");
                return;
            }
            m_client.ReactionAdded += ReactionChangedCallback;
            m_client.ReactionRemoved += ReactionChangedCallback;

            await Task.CompletedTask;
        }

        private async Task ReactionChangedCallback(Cacheable<IUserMessage, ulong> cache, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if (reaction.User.Value.Id == m_client.CurrentUser.Id)
                return;

            if (m_callbacks.UsedSpots == 0)
                return;

            IUserMessage message = await channel.GetMessageAsync(cache.Id) as IUserMessage;

            if (message.Author.Id != m_client.CurrentUser.Id)
                return;

            if (!ReactionEmotes.Any(message.Reactions.ContainsKey))
                return;

            var entry = m_callbacks.TryFindBackwards(tup => tup.Id == message.Id);
            if (!entry.HasValue)
                return;

            var callback = entry.Value.Callback;

            await callback.Invoke(message, GetMoveFromEmote(reaction.Emote));
        }

        private PaginationMove GetMoveFromEmote(IEmote emote)
        {
            if (emote.Name == First.Name)
                return PaginationMove.FIRST;
            if (emote.Name == Previous.Name)
                return PaginationMove.PREVIOUS;
            if (emote.Name == Next.Name)
                return PaginationMove.NEXT;
            if (emote.Name == Last.Name)
                return PaginationMove.LAST;

            throw new ArgumentException($"{emote.Name} is not a valid move emote");
        }

        public void AddCallback(ulong id, ReactionCallback callback)
        {
            if (m_callbacks.IsFull)
                m_callbacks.Free();
            m_callbacks.Insert((id, callback));
        }

        public void AddCallback(ulong id, int pageCount, Func<IUserMessage, int, Task> callback)
        {
            int i = 0;
            AddCallback(id, async (IUserMessage msg, PaginationMove move) =>
            {
                switch (move)
                {
                    case PaginationMove.FIRST:
                        i = 0;
                        break;
                    case PaginationMove.PREVIOUS:
                        if (i - 1 >= 0)
                            i--;
                        break;
                    case PaginationMove.NEXT:
                        if (i + 1 < pageCount)
                            i++;
                        break;
                    case PaginationMove.LAST:
                        i = pageCount - 1;
                        break;
                }
                await callback(msg, i);
            });
        }

        public async Task SendPaginatedDataMessageAsync<T>(IMessageChannel channel, T[] pageData, Func<T, int, EmbedFooterBuilder, Embed> getEmbed)
        {
            Embed GetEmbed(int i)
            {
                return getEmbed(pageData[i], i, new EmbedFooterBuilder().WithText($"page {i + 1} out of {pageData.Length}"));
            }

            if(pageData.Length <= 0)
                throw new ArgumentException("Passed zero length array");

            pageData = pageData.Where(result => result != null).ToArray();

            IUserMessage message = await m_communication.SendMessageAsync(channel, embed: GetEmbed(0));

            await message.AddReactionsAsync(ReactionEmotes);

            AddCallback(message.Id, pageData.Length, async (IUserMessage msg, int i) =>
            {
                await msg.ModifyAsync((MessageProperties properties) =>
                {
                    properties.Embed = Optional.Create(GetEmbed(i));
                });
            });
        }

        public async Task SendPaginatedDataAsyncMessageAsync<T>(IMessageChannel channel, T[] pageData, Func<T, int, EmbedFooterBuilder, Task<Embed>> getEmbed)
        {
            Task<Embed> GetEmbedTask(int i)
            {
                return getEmbed(pageData[i], i, new EmbedFooterBuilder().WithText($"page {i + 1} out of {pageData.Length}"));
            }

            if(pageData.Length <= 0)
                throw new ArgumentException("Passed zero length array");

            pageData = pageData.Where(result => result != null).ToArray();

            IUserMessage message = await m_communication.SendMessageAsync(channel, embed: await GetEmbedTask(0));

            await message.AddReactionsAsync(ReactionEmotes);

            AddCallback(message.Id, pageData.Length, async (IUserMessage msg, int i) =>
            {
                Embed embed = await GetEmbedTask(i);
                await msg.ModifyAsync((MessageProperties properties) =>
                {
                    properties.Embed = Optional.Create(embed);
                });
            });
        }

        public async Task SendPaginatedEmbedMessageAsync(IMessageChannel channel, EmbedBuilder[] embeds)
        {
            Embed GetEmbed(int i) => embeds[i]
                    .WithFooter(new EmbedFooterBuilder().WithText($"page {i + 1} out of {embeds.Length}"))
                    .Build();

            if(embeds.Length <= 0)
                throw new ArgumentException("Passed zero length array");

            IUserMessage message = await m_communication.SendMessageAsync(channel, embed: GetEmbed(0));

            await message.AddReactionsAsync(ReactionEmotes);

            AddCallback(message.Id, embeds.Length, async (IUserMessage msg, int i) =>
            {
                await msg.ModifyAsync((MessageProperties properties) =>
                {
                    properties.Embed = Optional.Create(GetEmbed(i));
                });
            });
        }
    }
    public enum PaginationMove
    {
        FIRST, PREVIOUS, NEXT, LAST
    }
}