using System.Text.Json;
using PhoneCardTracker.Models;

namespace PhoneCardTracker.Services
{
    public class NetworkSyncManager
    {
        private const string RawDbUrl = "https://raw.githubusercontent.com/HD25Happy/CardTracker-Assets/main/all_cards.json";
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly DatabaseService _databaseService;

        public NetworkSyncManager(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public bool HasInternetConnection()
        {
            return Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
        }

        public async Task<bool> SyncWithGitHubAsync()
        {
            if (!HasInternetConnection())
            {
                return false;
            }

            using var response = await _httpClient.GetAsync(RawDbUrl);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            string json = await response.Content.ReadAsStringAsync();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var masterCatalog = JsonSerializer.Deserialize<MasterDatabaseDto>(json, options);
            if (masterCatalog == null || masterCatalog.games == null) return false;

            var cardsToInsert = new List<Card>();

            foreach (KeyValuePair<string, List<MasterSetEntryDto>> gameEntry in masterCatalog.games)
            {
                string gameName = gameEntry.Key;
                List<MasterSetEntryDto> setList = gameEntry.Value;

                foreach (var set in setList)
                {
                    foreach (var cardDto in set.cards)
                    {
                        string safeEdition = string.IsNullOrEmpty(set.edition) ? "" : $"-{set.edition.ToLower().Replace(" ", "-")}";
                        string uniqueKey = $"{gameName.ToLower()}-{set.setId}{safeEdition}-{cardDto.cardNumber}";

                        cardsToInsert.Add(new Card
                        {
                            UniqueKey = uniqueKey,
                            Game = gameName,
                            SetName = set.setName,
                            SetWebUrl = set.setWebUrl,
                            Edition = set.edition,
                            Category = set.category,
                            CardName = cardDto.cardName,
                            CardNumber = cardDto.cardNumber,
                            Rarity = cardDto.rarity,
                            CardType = cardDto.cardType,
                            ImageUrl = cardDto.imageUrl
                        });
                    }
                }
            }

            await _databaseService.SaveOrUpdateCardsAsync(cardsToInsert);
            return true;
        }
    }
}