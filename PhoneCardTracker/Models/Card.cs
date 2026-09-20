using SQLite;

namespace PhoneCardTracker.Models
{
    [Table("Cards")]
    public class Card
    {
        [PrimaryKey]
        public string UniqueKey { get; set; } = string.Empty;

        [Indexed]
        public string Game { get; set; } = string.Empty;

        [Indexed]
        public string SetName { get; set; } = string.Empty;

        public string SetWebUrl { get; set; } = string.Empty;
        public string? Edition { get; set; }
        public string? Category { get; set; }
        public string CardName { get; set; } = string.Empty;
        public int CardNumber { get; set; }
        public string Rarity { get; set; } = "Common";
        public string CardType { get; set; } = "Standard";
        public string ImageUrl { get; set; } = string.Empty;

        // User collection progress
        public int OwnedCount { get; set; } = 0;
        public bool IsWishlist { get; set; } = false;
    }
}