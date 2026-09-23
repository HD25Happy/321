using PhoneCardTracker.Models;
using PhoneCardTracker.Services;

namespace PhoneCardTracker.Views
{
    [QueryProperty(nameof(SelectedGame), "game")]
    [QueryProperty(nameof(SelectedSet), "set")]
    public partial class CardListPage : ContentPage
    {
        private readonly DatabaseService _databaseService;
        private List<Card> _currentCards = new();

        public string SelectedGame { get; set; } = string.Empty;
        public string SelectedSet { get; set; } = string.Empty;

        private bool _isBinderView = false;
        public bool IsStandardView => !_isBinderView;

        private Card? _selectedCard;

        private int _currentPage = 1;
        private int _currentColumns = 3;
        private int _currentRows = 3;

        private double _cardImageHeight = 200;
        public double CardImageHeight
        {
            get => _cardImageHeight;
            set
            {
                _cardImageHeight = value;
                OnPropertyChanged();
            }
        }

        public CardListPage(DatabaseService databaseService)
        {
            InitializeComponent();
            _databaseService = databaseService;
        }

        protected override async void OnNavigatedTo(NavigatedToEventArgs args)
        {
            base.OnNavigatedTo(args);

            Title = SelectedSet;
            SetTitleLabel.Text = SelectedSet;
            SetSubtitleLabel.Text = $"{SelectedGame} • Loading cards...";

            await LoadCardsAsync();
        }

        private async Task LoadCardsAsync()
        {
            _currentCards = await _databaseService.GetCardsBySetAsync(SelectedGame, SelectedSet);
            UpdateCurrentView();
            UpdateProgressHeader();
        }

        private void UpdateProgressHeader()
        {
            if (_currentCards.Count == 0)
            {
                SetSubtitleLabel.Text = $"{SelectedGame} • No cards found";
                return;
            }

            int total = _currentCards.Count;
            int uniqueOwned = _currentCards.Count(c => c.IsOwned);
            int totalHolo = _currentCards.Sum(c => c.OwnedHolo);
            double percent = (double)uniqueOwned / total * 100.0;

            SetSubtitleLabel.Text = $"{SelectedGame} • {uniqueOwned}/{total} owned ({percent:F0}%) • ✨ {totalHolo} Holo";
        }

        private void OnToggleViewModeClicked(object? sender, EventArgs e)
        {
            _isBinderView = !_isBinderView;

            SettingsBtn.IsVisible = _isBinderView;
            PaginationBar.IsVisible = _isBinderView;
            ViewModeBtn.Text = _isBinderView ? "📋 List" : "📖 Binder";

            OnPropertyChanged(nameof(IsStandardView));

            if (_isBinderView)
            {
                _currentPage = 1;
                ApplyBinderSettings();
            }
            else
            {
                SettingsModalOverlay.IsVisible = false;
                CardDetailModal.IsVisible = false;
                CardsLayout.Span = 2;
                CardImageHeight = 200;
                CardsCollectionView.ItemsSource = _currentCards;
            }
        }

        private void OnBlockBackgroundTapped(object? sender, EventArgs e)
        {
            // Consumes event to prevent pass-through touches to background views
        }

        // --- CARD DETAIL POPUP ---
        private void OnCardTapped(object? sender, TappedEventArgs e)
        {
            if (e.Parameter is Card card)
            {
                _selectedCard = card;

                ModalCardNameLabel.Text = card.CardName;
                ModalCardDetailsLabel.Text = $"#{card.CardNumber} • {card.Rarity} • {card.CardType}";
                ModalCardImage.Source = card.ImageUrl;
                ModalNormalCountLabel.Text = card.OwnedNormal.ToString();
                ModalHoloCountLabel.Text = card.OwnedHolo.ToString();

                CardDetailModal.IsVisible = true;
            }
        }

        private void OnCloseCardDetailClicked(object? sender, EventArgs e)
        {
            CardDetailModal.IsVisible = false;
            _selectedCard = null;
        }

        private async void OnModalIncreaseNormalClicked(object? sender, EventArgs e)
        {
            if (_selectedCard != null)
            {
                _selectedCard.OwnedNormal++;
                ModalNormalCountLabel.Text = _selectedCard.OwnedNormal.ToString();
                await _databaseService.UpdateCardInventoryAsync(_selectedCard);
                UpdateProgressHeader();
            }
        }

        private async void OnModalDecreaseNormalClicked(object? sender, EventArgs e)
        {
            if (_selectedCard != null && _selectedCard.OwnedNormal > 0)
            {
                _selectedCard.OwnedNormal--;
                ModalNormalCountLabel.Text = _selectedCard.OwnedNormal.ToString();
                await _databaseService.UpdateCardInventoryAsync(_selectedCard);
                UpdateProgressHeader();
            }
        }

        private async void OnModalIncreaseHoloClicked(object? sender, EventArgs e)
        {
            if (_selectedCard != null)
            {
                _selectedCard.OwnedHolo++;
                ModalHoloCountLabel.Text = _selectedCard.OwnedHolo.ToString();
                await _databaseService.UpdateCardInventoryAsync(_selectedCard);
                UpdateProgressHeader();
            }
        }

        private async void OnModalDecreaseHoloClicked(object? sender, EventArgs e)
        {
            if (_selectedCard != null && _selectedCard.OwnedHolo > 0)
            {
                _selectedCard.OwnedHolo--;
                ModalHoloCountLabel.Text = _selectedCard.OwnedHolo.ToString();
                await _databaseService.UpdateCardInventoryAsync(_selectedCard);
                UpdateProgressHeader();
            }
        }

        // --- BINDER SETTINGS POPUP ---
        private void OnOpenSettingsClicked(object? sender, EventArgs e)
        {
            ModalColumnsStepper.Value = _currentColumns;
            ModalRowsStepper.Value = _currentRows;
            UpdateModalLabels();

            SettingsModalOverlay.IsVisible = true;
        }

        private void OnCloseSettingsClicked(object? sender, EventArgs e)
        {
            SettingsModalOverlay.IsVisible = false;
            _currentPage = 1;
            ApplyBinderSettings();
        }

        private void OnModalSettingsChanged(object? sender, ValueChangedEventArgs e)
        {
            _currentColumns = (int)Math.Clamp(Math.Round(ModalColumnsStepper.Value), 1, 5);
            _currentRows = (int)Math.Clamp(Math.Round(ModalRowsStepper.Value), 1, 5);

            UpdateModalLabels();
        }

        private void UpdateModalLabels()
        {
            ModalColumnsLabel.Text = $"Columns: {_currentColumns}";
            ModalRowsLabel.Text = $"Rows: {_currentRows}";
            ModalTotalCardsPreviewLabel.Text = $"{_currentColumns * _currentRows} cards per binder page";
        }

        private void ApplyBinderSettings()
        {
            CardsLayout.Span = _currentColumns;

            double baseHeight = _currentColumns switch
            {
                1 => 320,
                2 => 200,
                3 => 135,
                4 => 100,
                5 => 75,
                _ => 135
            };

            CardImageHeight = baseHeight * (0.70 + (_currentRows * 0.10));

            UpdateCurrentView();
        }

        private void UpdateCurrentView()
        {
            if (!_isBinderView)
            {
                CardsCollectionView.ItemsSource = _currentCards;
                return;
            }

            int pageSize = _currentColumns * _currentRows;
            if (pageSize <= 0) pageSize = 9;

            int totalPages = (int)Math.Ceiling((double)_currentCards.Count / pageSize);
            if (totalPages == 0) totalPages = 1;

            _currentPage = Math.Clamp(_currentPage, 1, totalPages);

            var pageCards = _currentCards
                .Skip((_currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            CardsCollectionView.ItemsSource = pageCards;

            PageIndicatorLabel.Text = $"Page {_currentPage} of {totalPages}";
            PrevPageBtn.IsEnabled = _currentPage > 1;
            NextPageBtn.IsEnabled = _currentPage < totalPages;
        }

        private void OnPrevPageClicked(object? sender, EventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                UpdateCurrentView();
            }
        }

        private void OnNextPageClicked(object? sender, EventArgs e)
        {
            int pageSize = _currentColumns * _currentRows;
            int totalPages = (int)Math.Ceiling((double)_currentCards.Count / pageSize);

            if (_currentPage < totalPages)
            {
                _currentPage++;
                UpdateCurrentView();
            }
        }

        private async void OnIncreaseNormalClicked(object? sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is Card card)
            {
                card.OwnedNormal++;
                await _databaseService.UpdateCardInventoryAsync(card);
                UpdateProgressHeader();
            }
        }

        private async void OnDecreaseNormalClicked(object? sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is Card card)
            {
                if (card.OwnedNormal > 0)
                {
                    card.OwnedNormal--;
                    await _databaseService.UpdateCardInventoryAsync(card);
                    UpdateProgressHeader();
                }
            }
        }

        private async void OnIncreaseHoloClicked(object? sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is Card card)
            {
                card.OwnedHolo++;
                await _databaseService.UpdateCardInventoryAsync(card);
                UpdateProgressHeader();
            }
        }

        private async void OnDecreaseHoloClicked(object? sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is Card card)
            {
                if (card.OwnedHolo > 0)
                {
                    card.OwnedHolo--;
                    await _databaseService.UpdateCardInventoryAsync(card);
                    UpdateProgressHeader();
                }
            }
        }
    }
}