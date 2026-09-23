using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PhoneCardTracker.Models
{
    public class CardSet : INotifyPropertyChanged
    {
        public string SetId { get; set; } = string.Empty;
        public string SetName { get; set; } = string.Empty;
        public string SetWebUrl { get; set; } = string.Empty;
        public string Game { get; set; } = string.Empty;
        public string? Edition { get; set; }
        public string? Category { get; set; }

        private bool _isFavorite;
        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (_isFavorite != value)
                {
                    _isFavorite = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FavoriteIcon));
                    OnPropertyChanged(nameof(FavoriteColor));
                }
            }
        }

        public string FavoriteIcon => IsFavorite ? "★" : "☆";
        public Color FavoriteColor => IsFavorite ? Color.FromArgb("#F59E0B") : Color.FromArgb("#9E9E9E");

        public List<Card> Cards { get; set; } = new();

        public int TotalCards => Cards.Count > 0 ? Cards.Count : _totalCards;
        private int _totalCards;
        public void SetTotalCards(int count) => _totalCards = count;

        public int OwnedCards => Cards.Count(c => c.OwnedCount > 0);
        public double Progress => TotalCards > 0 ? (double)OwnedCards / TotalCards : 0.0;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}