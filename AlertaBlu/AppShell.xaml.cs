namespace AlertaBlu;

public partial class AppShell : Shell
{
    /// <summary>
    /// Takes the page from the container rather than declaring a <c>ContentTemplate</c>, so
    /// <see cref="MainPage"/> gets its view model injected through its constructor.
    /// </summary>
    public AppShell(MainPage mainPage)
    {
        InitializeComponent();

        HomeContent.Content = mainPage;
    }
}
