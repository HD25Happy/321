namespace PhoneCardTracker.Models
{
    public class MasterDatabaseDto
    {
        public DateTime lastUpdated { get; set; }
        public Dictionary<string, List<MasterSetEntryDto>> games { get; set; } = new();
    }

    public class MasterSetEntryDto
    {
        public string setName { get; set; } = string.Empty;
        public string setWebUrl { get; set; } = string.Empty;
        public string setId { get; set; } = string.Empty;
        public string? edition { get; set; }
        public string? category { get; set; }
        public int totalCards { get; set; }
        public List<MasterCardEntryDto> cards { get; set; } = new();
    }

    public class MasterCardEntryDto
    {
        public string cardName { get; set; } = string.Empty;
        public int cardNumber { get; set; }
        public string rarity { get; set; } = "Common";
        public string cardType { get; set; } = "Standard";
        public string? edition { get; set; }
        public string imageUrl { get; set; } = string.Empty;
    }
}