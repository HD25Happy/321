using System.ComponentModel;
using System.Runtime.CompilerServices;
using SQLite;

namespace PhoneCardTracker.Models
{
    [Table("Cards")]
    public class Card : INotifyPropertyChanged
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

        private int _ownedNormal = 0;
        public int OwnedNormal
        {
            get => _ownedNormal;
            set
            {
                if (_ownedNormal != value)
                {
                    _ownedNormal = Math.Max(0, value);
                    OnPropertyChanged();
                    UpdateComputedProperties();
                }
            }
        }

        private int _ownedHolo = 0;
        public int OwnedHolo
        {
            get => _ownedHolo;
            set
            {
                if (_ownedHolo != value)
                {
                    _ownedHolo = Math.Max(0, value);
                    OnPropertyChanged();
                    UpdateComputedProperties();
                }
            }
        }

        public int OwnedCount
        {
            get => _ownedNormal + _ownedHolo;
            set
            {
                if (_ownedNormal + _ownedHolo != value)
                {
                    _ownedNormal = Math.Max(0, value);
                    OnPropertyChanged();
                    UpdateComputedProperties();
                }
            }
        }

        private bool _isWishlist = false;
        public bool IsWishlist
        {
            get => _isWishlist;
            set
            {
                if (_isWishlist != value)
                {
                    _isWishlist = value;
                    OnPropertyChanged();
                }
            }
        }

        [Ignore]
        public bool IsOwned => OwnedCount > 0;

        [Ignore]
        public bool IsNotOwned => !IsOwned;

        [Ignore]
        public bool HasHolo => OwnedHolo > 0;

        // Dæmpet/gråtonet hvis ikke ejet (35% opacity)
        [Ignore]
        public double CardOpacity => IsOwned ? 1.0 : 0.35;

        private void UpdateComputedProperties()
        {
            OnPropertyChanged(nameof(OwnedCount));
            OnPropertyChanged(nameof(IsOwned));
            OnPropertyChanged(nameof(IsNotOwned));
            OnPropertyChanged(nameof(HasHolo));
            OnPropertyChanged(nameof(CardOpacity));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}