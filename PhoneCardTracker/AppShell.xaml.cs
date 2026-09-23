using PhoneCardTracker.Views;

namespace PhoneCardTracker
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(CardListPage), typeof(CardListPage));
        }
    }
}