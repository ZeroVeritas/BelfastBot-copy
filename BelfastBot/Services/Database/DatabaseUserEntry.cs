using System;
using System.Collections.Generic;

namespace BelfastBot.Services.Database
{
    public class DatabaseUserEntry
    {
        public ulong Id { get; set; } = 0;
        public ulong Xp { get; set; } = 0;
        public uint Level => (uint)Math.Log(Xp + 80, 1.1) - 45;
        public List<Warn> Warns { get; set; } = new List<Warn>();
        public string AnilistName { get; set; } = null;
        public string OsuName { get; set; } = null;
        public uint? QuaverId { get; set; } = null;
        public uint Coins { get; set; } = 100;
        public int GachaRolls { get; set; } = 0;
        public List<GachaCard> Cards { get; set; } = new List<GachaCard>();
        public GachaCard FavoriteCard { get; set; } = null;
        public DateTime? LastDaily { get; set; } = null;

        public static DatabaseUserEntry CreateNew(ulong userId) => new DatabaseUserEntry
        {
            Id = userId,
        };
    }
}