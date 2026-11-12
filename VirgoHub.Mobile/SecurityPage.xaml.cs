using VirgoHub.Mobile.Services;

namespace VirgoHub.Mobile;

public partial class SecurityPage : ContentPage
{
    public SecurityPage(StudentPortalStore store)
    {
        InitializeComponent();
        Store = store;
        BindingContext = this;
    }

    public StudentPortalStore Store { get; }

    private async void OnBackTapped(object? sender, TappedEventArgs e) =>
        await Navigation.PopAsync();
}
