using SQLite;
using PhoneCardTracker.Models;

namespace PhoneCardTracker.Services
{
    public class DatabaseService
    {
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

        public async Task<List<Card>> GetCardsBySetAsync(string game, string setName)
        {
            await InitAsync();
            return await _database!.Table<Card>()
                .Where(c => c.Game == game && c.SetName == setName)
                .OrderBy(c => c.CardNumber)
                .ToListAsync();
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
                    card.OwnedCount = local.OwnedCount;
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