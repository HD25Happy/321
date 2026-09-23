using System.Diagnostics;
using System.Text;
using System.Text.Json;
using PhoneCardTracker.Models;

namespace PhoneCardTracker.Services
{
    public class CloudCardRecord
    {
        public int Normal { get; set; }
        public int Holo { get; set; }
    }

    public class CloudVaultData
    {
        public Dictionary<string, CloudCardRecord>? Inventory { get; set; }
        public List<string>? FavoriteSets { get; set; }
    }

    public class FirebaseSyncService
    {
        private const string PrefFirebaseUrlKey = "cloud_firebase_custom_url";
        private const string PrefProjectIdKey = "cloud_firebase_project_id";
        private const string PrefRegionKey = "cloud_firebase_region";
        private const string PrefUserIdKey = "cloud_sync_user_id";

        private readonly DatabaseService _databaseService;
        private readonly HttpClient _httpClient;

        public FirebaseSyncService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
            _httpClient = new HttpClient();
        }

        // Automatically construct the URL from Project ID and Region, or fallback to custom URL if set
        public string? GetFirebaseDatabaseUrl()
        {
            string? projectId = Preferences.Default.Get<string?>(PrefProjectIdKey, null);
            string? region = Preferences.Default.Get<string?>(PrefRegionKey, "europe-west1");

            if (!string.IsNullOrWhiteSpace(projectId))
            {
                string cleanId = projectId.Trim();
                // If user didn't type '-default-rtdb', append it automatically
                if (!cleanId.EndsWith("-default-rtdb", StringComparison.OrdinalIgnoreCase))
                {
                    cleanId += "-default-rtdb";
                }
                return $"https://{cleanId}.{region}.firebasedatabase.app/";
            }

            // Fallback for custom legacy URLs
            string? url = Preferences.Default.Get<string?>(PrefFirebaseUrlKey, null);
            return string.IsNullOrWhiteSpace(url) ? null : url.Trim();
        }

        public void SetFirebaseConfig(string projectId, string region)
        {
            if (string.IsNullOrWhiteSpace(projectId))
            {
                Preferences.Default.Remove(PrefProjectIdKey);
                Preferences.Default.Remove(PrefRegionKey);
                return;
            }

            Preferences.Default.Set(PrefProjectIdKey, projectId.Trim());
            Preferences.Default.Set(PrefRegionKey, region.Trim());
            Preferences.Default.Remove(PrefFirebaseUrlKey); // Clear legacy manual url
        }

        public string GetStoredProjectId()
        {
            return Preferences.Default.Get<string>(PrefProjectIdKey, "cardtrackercloud");
        }

        public string GetStoredRegion()
        {
            return Preferences.Default.Get<string>(PrefRegionKey, "europe-west1");
        }

        public string GetSyncUserId()
        {
            string? id = Preferences.Default.Get<string?>(PrefUserIdKey, null);
            if (string.IsNullOrWhiteSpace(id))
            {
                id = "MyVault1";
                Preferences.Default.Set(PrefUserIdKey, id);
            }
            return id;
        }

        public void SetSyncUserId(string newId)
        {
            if (!string.IsNullOrWhiteSpace(newId))
            {
                Preferences.Default.Set(PrefUserIdKey, newId.Trim());
            }
        }

        // PUSH: Uploads owned cards and favorite sets to Firebase
        public async Task<(bool Success, string Message)> UploadInventoryToCloudAsync()
        {
            string? baseUrl = GetFirebaseDatabaseUrl();
            if (string.IsNullOrEmpty(baseUrl))
            {
                return (false, "No Firebase Project ID set. Configure your settings first.");
            }

            try
            {
                string userId = GetSyncUserId();
                var allCards = await _databaseService.GetAllCardsAsync();

                var ownedDict = allCards
                    .Where(c => c.OwnedNormal > 0 || c.OwnedHolo > 0)
                    .ToDictionary(
                        c => SanitizeFirebaseKey(c.UniqueKey),
                        c => new CloudCardRecord { Normal = c.OwnedNormal, Holo = c.OwnedHolo }
                    );

                var favoriteSets = await _databaseService.GetAllFavoriteSetsAsync();
                var favoriteIdentifiers = favoriteSets
                    .Select(s => $"{s.Game}|{s.SetName}")
                    .Distinct()
                    .ToList();

                var vaultData = new CloudVaultData
                {
                    Inventory = ownedDict,
                    FavoriteSets = favoriteIdentifiers
                };

                string jsonPayload = JsonSerializer.Serialize(vaultData);
                string endpoint = $"{baseUrl}users/{Uri.EscapeDataString(userId)}.json";

                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync(endpoint, content);

                if (response.IsSuccessStatusCode)
                {
                    return (true, $"Uploaded {ownedDict.Count} cards & {favoriteIdentifiers.Count} favorites to Cloud (Vault: {userId})");
                }

                return (false, $"Firebase Error: {(int)response.StatusCode} ({response.ReasonPhrase})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Firebase Upload Error] {ex.Message}");
                return (false, $"Upload failed: {ex.Message}");
            }
        }

        // PULL: Downloads cloud inventory and favorites with resilient 404 handling
        public async Task<(bool Success, string Message)> DownloadInventoryFromCloudAsync()
        {
            string? baseUrl = GetFirebaseDatabaseUrl();
            if (string.IsNullOrEmpty(baseUrl))
            {
                return (false, "No Firebase Project ID set. Configure your settings first.");
            }

            try
            {
                string userId = GetSyncUserId();
                string endpoint = $"{baseUrl}users/{Uri.EscapeDataString(userId)}.json";

                var response = await _httpClient.GetAsync(endpoint);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return (true, "Cloud vault is empty or hasn't been uploaded to yet.");
                }

                if (!response.IsSuccessStatusCode)
                {
                    return (false, $"Firebase Error: {(int)response.StatusCode} ({response.ReasonPhrase})");
                }

                string jsonContent = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(jsonContent) || jsonContent == "null")
                {
                    return (true, "Cloud vault is empty.");
                }

                var vaultData = JsonSerializer.Deserialize<CloudVaultData>(jsonContent);
                if (vaultData == null || (vaultData.Inventory == null && vaultData.FavoriteSets == null))
                {
                    return (true, "No card records or favorites found in cloud.");
                }

                int updatedCards = 0;
                if (vaultData.Inventory != null)
                {
                    var allLocalCards = await _databaseService.GetAllCardsAsync();
                    foreach (var card in allLocalCards)
                    {
                        string safeKey = SanitizeFirebaseKey(card.UniqueKey);

                        if (vaultData.Inventory.TryGetValue(safeKey, out var record))
                        {
                            if (card.OwnedNormal != record.Normal || card.OwnedHolo != record.Holo)
                            {
                                card.OwnedNormal = record.Normal;
                                card.OwnedHolo = record.Holo;
                                await _databaseService.UpdateCardInventoryAsync(card);
                                updatedCards++;
                            }
                        }
                    }
                }

                int updatedFavorites = 0;
                if (vaultData.FavoriteSets != null)
                {
                    var currentFavorites = await _databaseService.GetAllFavoriteSetsAsync();
                    var currentSetKeys = new HashSet<string>(currentFavorites.Select(s => $"{s.Game}|{s.SetName}"));
                    var cloudSetKeys = new HashSet<string>(vaultData.FavoriteSets);

                    foreach (var cloudKey in cloudSetKeys)
                    {
                        var parts = cloudKey.Split('|');
                        if (parts.Length == 2)
                        {
                            string game = parts[0];
                            string setName = parts[1];

                            if (!currentSetKeys.Contains(cloudKey))
                            {
                                _databaseService.SetDefaultFavorite(game, setName);
                                updatedFavorites++;
                            }
                        }
                    }
                }

                return (true, $"Synced from Cloud: Updated {updatedCards} cards & {updatedFavorites} favorites!");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Firebase Download Error] {ex.Message}");
                return (false, $"Download failed: {ex.Message}");
            }
        }

        private static string SanitizeFirebaseKey(string key)
        {
            return key.Replace(".", "_")
                      .Replace("#", "_")
                      .Replace("$", "_")
                      .Replace("[", "_")
                      .Replace("]", "_")
                      .Replace("/", "_");
        }
    }
}