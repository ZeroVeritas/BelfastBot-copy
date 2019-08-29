﻿using System.Collections.Generic;

namespace SenkoSanBot.Services.Database
{
    public class DatabaseUserEntry
    {
        public ulong Id { get; set; } = 0;
        public List<Warn> Warns { get; set; } = new List<Warn>();
        public string OsuName { get; set; } = null;
        public int Coins { get; set; } = 100;
        public int GachaRolls { get; set; } = 0;
        public List<GachaCard> Cards { get; set; } = new List<GachaCard>();

        public static DatabaseUserEntry CreateNew(ulong userId) => new DatabaseUserEntry
        {
            Id = userId,
        };
    }
}