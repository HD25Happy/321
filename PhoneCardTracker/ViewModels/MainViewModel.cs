using System.Collections.ObjectModel;
using System.Text.Json;
using PhoneCardTracker.Models;

namespace PhoneCardTracker.ViewModels
{
    public class MainViewModel
    {
        // ObservableCollection automatically updates the UI when cards are added
        public ObservableCollection<Card> DisplayedCards { get; set; } = new ObservableCollection<Card>();

        public MainViewModel()
        {
            // Load the data as soon as the ViewModel starts up
            _ = LoadSetDataAsync("pokemon_base_set.json");
        }

        public async Task LoadSetDataAsync(string fileName)
        {
            try
            {
                // OpenAppPackageFileAsync reads files from the Resources/Raw folder
                using var stream = await FileSystem.OpenAppPackageFileAsync(fileName);

                // Deserialize the JSON directly into our CardSet model
                var cardSet = await JsonSerializer.DeserializeAsync<CardSet>(stream);

                if (cardSet != null && cardSet.Cards != null)
                {
                    DisplayedCards.Clear();
                    foreach (var card in cardSet.Cards)
                    {
                        DisplayedCards.Add(card);
                    }
                }
            }
            catch (Exception ex)
            {
                // In a real app, you would want to log this or show an error popup
                Console.WriteLine($"Error loading cards: {ex.Message}");
            }
        }
    }
}