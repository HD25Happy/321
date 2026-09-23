using PhoneCardTracker.Models;
using PhoneCardTracker.Services;

namespace PhoneCardTracker.Views
{
    public partial class FavoritesPage : ContentPage
    {
        private readonly DatabaseService _databaseService;

        public FavoritesPage(DatabaseService databaseService)
        {
            InitializeComponent();
            _databaseService = databaseService;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadFavoritesAsync();
        }

        private async Task LoadFavoritesAsync()
        {
            var favSets = await _databaseService.GetAllFavoriteSetsAsync();
            FavCollectionView.ItemsSource = favSets;

            SubtitleLabel.Text = favSets.Count == 1
                ? "1 favoritsæt gemt"
                : $"{favSets.Count} favoritsæt gemt";
        }

        private async void OnSetTapped(object? sender, TappedEventArgs e)
        {
            if (e.Parameter is CardSet set)
            {
                await Shell.Current.GoToAsync($"CardListPage?game={Uri.EscapeDataString(set.Game)}&set={Uri.EscapeDataString(set.SetName)}");
            }
        }

        private async void OnUnfavoriteClicked(object? sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is CardSet set)
            {
                _databaseService.ToggleSetFavorite(set);
                await LoadFavoritesAsync();
            }
        }
    }
}