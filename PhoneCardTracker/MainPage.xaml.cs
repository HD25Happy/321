using PhoneCardTracker.Models;
using PhoneCardTracker.Services;
using PhoneCardTracker.ViewModels;

namespace PhoneCardTracker;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _vm;
    private readonly DatabaseService _databaseService;
    private readonly NetworkSyncManager _syncManager;
    private readonly FirebaseSyncService _firebaseSync;

    public MainPage(
        MainViewModel vm,
        DatabaseService databaseService,
        NetworkSyncManager syncManager,
        FirebaseSyncService firebaseSync)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _databaseService = databaseService;
        _syncManager = syncManager;
        _firebaseSync = firebaseSync;
    }

    private async void OnSetTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is CardSet set)
        {
            await Shell.Current.GoToAsync($"CardListPage?game={Uri.EscapeDataString(set.Game)}&set={Uri.EscapeDataString(set.SetName)}");
        }
    }

    private async void OnSyncClicked(object? sender, EventArgs e)
    {
        await _vm.SyncDataFromGitHubAsync();

        if (_syncManager.LastFetchedCards.Count > 0)
        {
            var obsoleteReports = await _databaseService.CheckForObsoleteCardsAsync(_syncManager.LastFetchedCards);

            if (obsoleteReports.Count > 0)
            {
                ObsoleteItemsCollectionView.ItemsSource = obsoleteReports;
                ObsoleteItemsModal.IsVisible = true;
            }
        }

        if (!string.IsNullOrEmpty(_vm.SelectedGame))
        {
            await _vm.LoadSetsForGameAsync(_vm.SelectedGame);
        }
    }

    private async void OnCloudSyncClicked(object? sender, EventArgs e)
    {
        string? databaseUrl = _firebaseSync.GetFirebaseDatabaseUrl();

        // 1. If no Firebase configuration is set, prompt for Project ID and Region manually
        if (string.IsNullOrWhiteSpace(databaseUrl))
        {
            string projectId = await DisplayPromptAsync(
                "Firebase Setup",
                "Enter your Firebase Project ID (e.g. cardtrackercloud):",
                initialValue: _firebaseSync.GetStoredProjectId(),
                accept: "Next",
                cancel: "Cancel");

            if (string.IsNullOrWhiteSpace(projectId)) return;

            string region = await DisplayActionSheet(
                "Select Database Region",
                "Cancel",
                null,
                "europe-west1",
                "us-central1",
                "asia-southeast1");

            if (string.IsNullOrWhiteSpace(region) || region == "Cancel") return;

            _firebaseSync.SetFirebaseConfig(projectId, region);
            await DisplayAlert("Saved", "Firebase settings configured successfully!", "OK");
        }

        // 2. Open normal Cloud Sync menu (without camera options)
        string currentVault = _firebaseSync.GetSyncUserId();

        string action = await DisplayActionSheet(
            $"Cloud Sync (Vault: {currentVault})",
            "Cancel",
            null,
            "⬆ Upload Local Inventory to Cloud",
            "⬇ Download Cloud Inventory to This Device",
            "🔑 Change Sync Vault Key",
            "🌐 Configure Firebase Project ID & Region");

        if (action == "⬆ Upload Local Inventory to Cloud")
        {
            _vm.IsBusy = true;
            _vm.StatusMessage = "Uploading collection to Firebase...";
            var (success, msg) = await _firebaseSync.UploadInventoryToCloudAsync();
            _vm.StatusMessage = msg;
            _vm.IsBusy = false;
        }
        else if (action == "⬇ Download Cloud Inventory to This Device")
        {
            _vm.IsBusy = true;
            _vm.StatusMessage = "Downloading collection from Firebase...";
            var (success, msg) = await _firebaseSync.DownloadInventoryFromCloudAsync();
            _vm.StatusMessage = msg;
            _vm.IsBusy = false;

            if (!string.IsNullOrEmpty(_vm.SelectedGame))
            {
                await _vm.LoadSetsForGameAsync(_vm.SelectedGame);
            }
        }
        else if (action == "🔑 Change Sync Vault Key")
        {
            string newKey = await DisplayPromptAsync(
                "Sync Vault Key",
                "Enter a shared key (use the exact same key across your devices):",
                initialValue: currentVault);

            if (!string.IsNullOrWhiteSpace(newKey))
            {
                _firebaseSync.SetSyncUserId(newKey);
                _vm.StatusMessage = $"Active Vault: {newKey.Trim()}";
            }
        }
        else if (action == "🌐 Configure Firebase Project ID & Region")
        {
            string currentProj = _firebaseSync.GetStoredProjectId();
            string updatedProj = await DisplayPromptAsync(
                "Firebase Project ID",
                "Update your Firebase Project ID:",
                initialValue: currentProj,
                accept: "Next",
                cancel: "Cancel");

            if (string.IsNullOrWhiteSpace(updatedProj)) return;

            string region = await DisplayActionSheet(
                "Select Database Region",
                "Cancel",
                null,
                "europe-west1",
                "us-central1",
                "asia-southeast1");

            if (string.IsNullOrWhiteSpace(region) || region == "Cancel") return;

            _firebaseSync.SetFirebaseConfig(updatedProj, region);
            _vm.StatusMessage = "Firebase configuration updated!";
        }
    }

    private async void OnConfirmRemoveObsoleteClicked(object? sender, EventArgs e)
    {
        ObsoleteItemsModal.IsVisible = false;

        if (_syncManager.LastFetchedCards.Count > 0)
        {
            await _databaseService.RemoveObsoleteCardsAsync(_syncManager.LastFetchedCards);
            _vm.StatusMessage = "Obsolete items removed successfully!";

            await _vm.LoadGamesFromLocalDbAsync();
            if (!string.IsNullOrEmpty(_vm.SelectedGame))
            {
                await _vm.LoadSetsForGameAsync(_vm.SelectedGame);
            }
        }
    }

    private void OnDismissObsoleteClicked(object? sender, EventArgs e)
    {
        ObsoleteItemsModal.IsVisible = false;
    }

    private void OnBlockTapped(object? sender, EventArgs e)
    {
        // Consumes tap events to prevent interacting with background views when modal is active
    }

    private void OnFavoriteStarClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is CardSet set)
        {
            _vm.ToggleFavorite(set);
        }
    }
}