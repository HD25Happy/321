namespace PhoneCardTracker.Models
{
    public class CardSet
    {
        public string SetId { get; set; } = string.Empty;
        public string SetName { get; set; } = string.Empty;
        public string SetWebUrl { get; set; } = string.Empty;
        public string Game { get; set; } = string.Empty;
        public string? Edition { get; set; }
        public string? Category { get; set; }

        public List<Card> Cards { get; set; } = new();

        public int TotalCards => Cards.Count > 0 ? Cards.Count : _totalCards;
        private int _totalCards;
        public void SetTotalCards(int count) => _totalCards = count;

        public int OwnedCards => Cards.Count(c => c.OwnedCount > 0);
        public double Progress => TotalCards > 0 ? (double)OwnedCards / TotalCards : 0.0;
    }
}