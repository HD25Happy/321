namespace PhoneCardTracker;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Starter appen op med MainPage inde i vinduet
        return new Window(new MainPage());
    }
}