using SQLite;
using PhoneCardTracker.Models;

namespace PhoneCardTracker.Services
{
    public class ObsoleteItemReport
    {
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
    }

    public class DatabaseService
    {
        private const string PrefDefaultGameKey = "default_favorite_game";
        private const string PrefDefaultSetKey = "default_favorite_set";

        private SQLiteAsyncConnection? _database;

        private async Task InitAsync()
        {
            if (_database != null) return;

            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "PhoneCardTracker.db3");
            _database = new SQLiteAsyncConnection(dbPath);
            await _database.CreateTableAsync<Card>();
        }

        public async Task<List<Card>> GetAllCardsAsync()
        {
            await InitAsync();
            return await _database!.Table<Card>().ToListAsync();
        }

        public async Task<List<string>> GetAllGamesAsync()
        {
            await InitAsync();
            var cards = await _database!.Table<Card>().ToListAsync();
            return cards.Select(c => c.Game)
                        .Where(g => !string.IsNullOrWhiteSpace(g))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(g => g)
                        .ToList();
        }

        public async Task<List<CardSet>> GetSetsByGameAsync(string game)
        {
            await InitAsync();

            var (favGame, favSet) = GetDefaultFavoriteSet();

            var cardsInGame = await _database!.Table<Card>()
                .Where(c => c.Game.ToLower() == game.ToLower())
                .ToListAsync();

            var sets = cardsInGame
                .GroupBy(c => c.SetName)
                .Select(g =>
                {
                    string setName = g.Key;
                    bool isFav = Preferences.Default.Get<bool>(GetFavoriteKey(game, setName), false) ||
                                 (string.Equals(favGame, game, StringComparison.OrdinalIgnoreCase) &&
                                  string.Equals(favSet, setName, StringComparison.OrdinalIgnoreCase));

                    return new CardSet
                    {
                        SetName = setName,
                        Game = game,
                        SetWebUrl = g.First().SetWebUrl,
                        Edition = g.First().Edition,
                        Category = g.First().Category,
                        Cards = g.ToList(),
                        IsFavorite = isFav
                    };
                })
                .OrderByDescending(s => s.IsFavorite)
                .ThenBy(s => s.SetName)
                .ToList();

            return sets;
        }

        public async Task<List<CardSet>> GetAllFavoriteSetsAsync()
        {
            await InitAsync();

            var (favGame, favSet) = GetDefaultFavoriteSet();
            var allCards = await _database!.Table<Card>().ToListAsync();

            var favoriteSets = allCards
                .GroupBy(c => new { c.Game, c.SetName })
                .Where(g => Preferences.Default.Get<bool>(GetFavoriteKey(g.Key.Game, g.Key.SetName), false) ||
                            (string.Equals(favGame, g.Key.Game, StringComparison.OrdinalIgnoreCase) &&
                             string.Equals(favSet, g.Key.SetName, StringComparison.OrdinalIgnoreCase)))
                .Select(g => new CardSet
                {
                    Game = g.Key.Game,
                    SetName = g.Key.SetName,
                    SetWebUrl = g.First().SetWebUrl,
                    Edition = g.First().Edition,
                    Category = g.First().Category,
                    Cards = g.ToList(),
                    IsFavorite = true
                })
                .OrderBy(s => s.Game)
                .ThenBy(s => s.SetName)
                .ToList();

            return favoriteSets;
        }

        public void ToggleSetFavorite(CardSet set)
        {
            set.IsFavorite = !set.IsFavorite;
            Preferences.Default.Set<bool>(GetFavoriteKey(set.Game, set.SetName), set.IsFavorite);

            if (set.IsFavorite)
            {
                SetDefaultFavorite(set.Game, set.SetName);
            }
            else
            {
                var (curGame, curSet) = GetDefaultFavoriteSet();
                if (string.Equals(curGame, set.Game, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(curSet, set.SetName, StringComparison.OrdinalIgnoreCase))
                {
                    ClearDefaultFavorite();
                }
            }
        }

        public void SetDefaultFavorite(string game, string setName)
        {
            Preferences.Default.Set(PrefDefaultGameKey, game);
            Preferences.Default.Set(PrefDefaultSetKey, setName);
            Preferences.Default.Set(GetFavoriteKey(game, setName), true);
        }

        public void ClearDefaultFavorite()
        {
            Preferences.Default.Remove(PrefDefaultGameKey);
            Preferences.Default.Remove(PrefDefaultSetKey);
        }

        public (string? Game, string? SetName) GetDefaultFavoriteSet()
        {
            string? game = Preferences.Default.Get<string?>(PrefDefaultGameKey, null);
            string? setName = Preferences.Default.Get<string?>(PrefDefaultSetKey, null);

            if (string.IsNullOrWhiteSpace(game) || string.IsNullOrWhiteSpace(setName))
            {
                return (null, null);
            }

            return (game, setName);
        }

        private static string GetFavoriteKey(string game, string setName)
        {
            return $"fav_{game.ToLower().Trim()}_{setName.ToLower().Trim()}";
        }

        public async Task<List<Card>> GetCardsBySetAsync(string game, string setName)
        {
            await InitAsync();
            return await _database!.Table<Card>()
                .Where(c => c.Game.ToLower() == game.ToLower() && c.SetName.ToLower() == setName.ToLower())
                .OrderBy(c => c.CardNumber)
                .ToListAsync();
        }

        // Returns obsolete local items not found in the incoming GitHub payload
        public async Task<List<ObsoleteItemReport>> CheckForObsoleteCardsAsync(List<Card> incomingCards)
        {
            await InitAsync();

            var existingCards = await _database!.Table<Card>().ToListAsync();
            var incomingKeys = new HashSet<string>(incomingCards.Select(c => c.UniqueKey));

            var obsoleteCards = existingCards.Where(c => !incomingKeys.Contains(c.UniqueKey)).ToList();

            // Group by Set to create readable summary items
            var report = obsoleteCards
                .GroupBy(c => new { c.Game, c.SetName })
                .Select(g => new ObsoleteItemReport
                {
                    Title = $"{g.Key.SetName}",
                    Subtitle = $"Game: {g.Key.Game} • {g.Count()} card(s) to remove"
                })
                .OrderBy(r => r.Title)
                .ToList();

            return report;
        }

        public async Task RemoveObsoleteCardsAsync(List<Card> incomingCards)
        {
            await InitAsync();

            var existingCards = await _database!.Table<Card>().ToListAsync();
            var incomingKeys = new HashSet<string>(incomingCards.Select(c => c.UniqueKey));

            await _database!.RunInTransactionAsync(tran =>
            {
                foreach (var card in existingCards)
                {
                    if (!incomingKeys.Contains(card.UniqueKey))
                    {
                        tran.Delete(card);
                    }
                }
            });
        }

        public async Task SaveOrUpdateCardsAsync(List<Card> incomingCards)
        {
            await InitAsync();

            var existingCards = await _database!.Table<Card>().ToListAsync();
            var existingDict = existingCards.ToDictionary(c => c.UniqueKey, c => c);

            foreach (var card in incomingCards)
            {
                if (existingDict.TryGetValue(card.UniqueKey, out var local))
                {
                    card.OwnedNormal = local.OwnedNormal;
                    card.OwnedHolo = local.OwnedHolo;
                    card.IsWishlist = local.IsWishlist;
                }
            }

            await _database!.RunInTransactionAsync(tran =>
            {
                foreach (var card in incomingCards)
                {
                    tran.InsertOrReplace(card);
                }
            });
        }

        public async Task UpdateCardInventoryAsync(Card card)
        {
            await InitAsync();
            await _database!.UpdateAsync(card);
        }
    }
}