using System.Diagnostics;
using System.Text.Json;
using PhoneCardTracker.Models;

namespace PhoneCardTracker.Services
{
    public class NetworkSyncManager
    {
        private const string GithubUser = "Hd25Happy";
        private const string GithubRepo = "CardTracker-Assets";

        private readonly DatabaseService _databaseService;
        private readonly HttpClient _httpClient;

        public List<Card> LastFetchedCards { get; private set; } = new();
        public string LastErrorMessage { get; private set; } = string.Empty;

        public NetworkSyncManager(DatabaseService databaseService)
        {
            _databaseService = databaseService;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "PhoneCardTrackerApp");
        }

        public async Task<bool> SyncWithGitHubAsync()
        {
            LastErrorMessage = string.Empty;

            // Check root first, fallback to Downloaded_Sets if necessary
            string[] candidateUrls = new[]
            {
                $"https://raw.githubusercontent.com/{GithubUser}/{GithubRepo}/main/all_cards.json?t={DateTime.UtcNow.Ticks}",
                $"https://raw.githubusercontent.com/{GithubUser}/{GithubRepo}/main/Downloaded_Sets/all_cards.json?t={DateTime.UtcNow.Ticks}"
            };

            HttpResponseMessage? response = null;
            string usedUrl = string.Empty;

            foreach (var url in candidateUrls)
            {
                try
                {
                    var res = await _httpClient.GetAsync(url);
                    if (res.IsSuccessStatusCode)
                    {
                        response = res;
                        usedUrl = url;
                        break;
                    }
                    else if ((int)res.StatusCode != 404)
                    {
                        // Any non-404 error (e.g. rate limit, bad gateway)
                        response = res;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    LastErrorMessage = $"Network connection error: {ex.Message}";
                }
            }

            if (response == null || !response.IsSuccessStatusCode)
            {
                if (string.IsNullOrEmpty(LastErrorMessage) && response != null)
                {
                    LastErrorMessage = $"GitHub returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase})";
                }
                else if (string.IsNullOrEmpty(LastErrorMessage))
                {
                    LastErrorMessage = "Could not connect to GitHub repository.";
                }
                return false;
            }

            try
            {
                string jsonContent = await response.Content.ReadAsStringAsync();

                var options = new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                };

                using var doc = JsonDocument.Parse(jsonContent, options);

                if (!doc.RootElement.TryGetProperty("games", out var gamesElement))
                {
                    LastErrorMessage = "Invalid all_cards.json format: 'games' property missing.";
                    return false;
                }

                var parsedCards = new List<Card>();

                foreach (var gameProperty in gamesElement.EnumerateObject())
                {
                    string gameName = gameProperty.Name;

                    if (gameProperty.Value.ValueKind != JsonValueKind.Array) continue;

                    foreach (var setElement in gameProperty.Value.EnumerateArray())
                    {
                        string setName = setElement.TryGetProperty("setName", out var sn) ? sn.GetString() ?? "" : "";
                        string setWebUrl = setElement.TryGetProperty("setWebUrl", out var swu) ? swu.GetString() ?? "" : "";
                        string? edition = setElement.TryGetProperty("edition", out var ed) ? ed.GetString() : null;
                        string? category = setElement.TryGetProperty("category", out var cat) ? cat.GetString() : null;

                        if (setElement.TryGetProperty("cards", out var cardsArray) && cardsArray.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var cardElement in cardsArray.EnumerateArray())
                            {
                                string cardName = cardElement.TryGetProperty("cardName", out var cn) ? cn.GetString() ?? "" : "";

                                int cardNumber = 0;
                                if (cardElement.TryGetProperty("cardNumber", out var numProp))
                                {
                                    if (numProp.ValueKind == JsonValueKind.Number)
                                        numProp.TryGetInt32(out cardNumber);
                                    else if (numProp.ValueKind == JsonValueKind.String)
                                        int.TryParse(numProp.GetString(), out cardNumber);
                                }

                                string rarity = cardElement.TryGetProperty("rarity", out var r) ? r.GetString() ?? "Common" : "Common";
                                string cardType = cardElement.TryGetProperty("cardType", out var ct) ? ct.GetString() ?? "Standard" : "Standard";
                                string imageUrl = cardElement.TryGetProperty("imageUrl", out var iu) ? iu.GetString() ?? "" : "";
                                string? cardEdition = cardElement.TryGetProperty("edition", out var ced) ? ced.GetString() : edition;

                                string uniqueKey = $"{gameName}_{setName}_{cardNumber}_{cardName}".ToLower().Trim();

                                parsedCards.Add(new Card
                                {
                                    UniqueKey = uniqueKey,
                                    Game = gameName,
                                    SetName = setName,
                                    SetWebUrl = setWebUrl,
                                    Edition = cardEdition,
                                    Category = category,
                                    CardName = cardName,
                                    CardNumber = cardNumber,
                                    Rarity = rarity,
                                    CardType = cardType,
                                    ImageUrl = imageUrl
                                });
                            }
                        }
                    }
                }

                LastFetchedCards = parsedCards;
                await _databaseService.SaveOrUpdateCardsAsync(parsedCards);
                return true;
            }
            catch (Exception ex)
            {
                LastErrorMessage = $"JSON parsing error: {ex.Message}";
                Debug.WriteLine($"[Sync] {LastErrorMessage}");
                return false;
            }
        }
    }
}