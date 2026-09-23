using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PhoneCardTracker.Models;
using PhoneCardTracker.Services;

namespace PhoneCardTracker.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private const string PrefLastSelectedGameKey = "last_selected_game";
        public const string FavoritesDropdownItem = "★ Favorites";

        private readonly DatabaseService _dbService;
        private readonly NetworkSyncManager _syncManager;
        private readonly List<CardSet> _allAvailableSets = new();

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged();
                    FilterSets();
                }
            }
        }

        private string? _selectedGame;
        public string? SelectedGame
        {
            get => _selectedGame;
            set
            {
                if (_selectedGame != value)
                {
                    _selectedGame = value;
                    OnPropertyChanged();
                    if (!string.IsNullOrWhiteSpace(_selectedGame))
                    {
                        Preferences.Default.Set(PrefLastSelectedGameKey, _selectedGame);
                        _ = LoadSetsForGameAsync(_selectedGame);
                    }
                }
            }
        }

        public ObservableCollection<string> Games { get; set; } = new();
        public ObservableCollection<CardSet> AvailableSets { get; set; } = new();

        public MainViewModel(DatabaseService dbService, NetworkSyncManager syncManager)
        {
            _dbService = dbService;
            _syncManager = syncManager;

            _ = InitializeAsync();
        }

        public async Task InitializeAsync()
        {
            await LoadGamesFromLocalDbAsync();
            await SyncDataFromGitHubAsync();
        }

        public async Task SyncDataFromGitHubAsync()
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;
                StatusMessage = "Fetching card data from GitHub...";

                bool success = await _syncManager.SyncWithGitHubAsync();

                if (success)
                {
                    StatusMessage = $"Sync complete! ({_syncManager.LastFetchedCards.Count} cards)";
                    await LoadGamesFromLocalDbAsync();

                    if (!string.IsNullOrEmpty(SelectedGame))
                    {
                        await LoadSetsForGameAsync(SelectedGame);
                    }
                }
                else
                {
                    StatusMessage = string.IsNullOrEmpty(_syncManager.LastErrorMessage)
                        ? "Sync failed (using local data)."
                        : _syncManager.LastErrorMessage;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task LoadGamesFromLocalDbAsync()
        {
            var dbGames = await _dbService.GetAllGamesAsync();

            Games.Clear();
            Games.Add(FavoritesDropdownItem);

            foreach (var g in dbGames)
            {
                Games.Add(g);
            }

            string savedGame = Preferences.Default.Get<string>(PrefLastSelectedGameKey, FavoritesDropdownItem);

            if (Games.Contains(savedGame))
            {
                _selectedGame = savedGame;
                OnPropertyChanged(nameof(SelectedGame));
                await LoadSetsForGameAsync(savedGame);
            }
            else
            {
                _selectedGame = FavoritesDropdownItem;
                OnPropertyChanged(nameof(SelectedGame));
                await LoadSetsForGameAsync(FavoritesDropdownItem);
            }
        }

        public async Task LoadSetsForGameAsync(string game)
        {
            if (string.IsNullOrWhiteSpace(game)) return;

            IsBusy = true;
            _allAvailableSets.Clear();
            AvailableSets.Clear();
            SearchText = string.Empty;

            try
            {
                List<CardSet> sets;

                if (game == FavoritesDropdownItem)
                {
                    sets = await _dbService.GetAllFavoriteSetsAsync();
                }
                else
                {
                    sets = await _dbService.GetSetsByGameAsync(game);
                }

                _allAvailableSets.AddRange(sets);
                FilterSets();
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void ToggleFavorite(CardSet set)
        {
            _dbService.ToggleSetFavorite(set);

            if (SelectedGame == FavoritesDropdownItem)
            {
                if (!set.IsFavorite)
                {
                    _allAvailableSets.Remove(set);
                }
            }
            else
            {
                var sorted = _allAvailableSets.OrderByDescending(s => s.IsFavorite).ThenBy(s => s.SetName).ToList();
                _allAvailableSets.Clear();
                _allAvailableSets.AddRange(sorted);
            }

            FilterSets();
        }

        public void FilterSets()
        {
            AvailableSets.Clear();

            var query = _allAvailableSets.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                query = query.Where(s => s.SetName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var set in query)
            {
                AvailableSets.Add(set);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}