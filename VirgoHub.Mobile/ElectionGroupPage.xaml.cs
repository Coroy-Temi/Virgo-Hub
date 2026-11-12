using System.Collections.ObjectModel;
using VirgoHub.Mobile.Services;
using VirgoHub.Shared;

namespace VirgoHub.Mobile;

public partial class ElectionGroupPage : ContentPage
{
    private readonly string _electionTitle;

    public ElectionGroupPage(StudentPortalStore store, string electionTitle)
    {
        InitializeComponent();
        Store = store;
        _electionTitle = electionTitle;
        BindingContext = this;
    }

    public StudentPortalStore Store { get; }

    public ObservableCollection<Election> Positions { get; } = [];

    public string ElectionTitle => _electionTitle;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Positions.Clear();
        foreach (var item in Store.ActiveElections.Where(x => string.Equals(x.Title, _electionTitle, StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.Position))
        {
            Positions.Add(item);
        }
    }

    private async void OnOpenPositionClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: Election election })
        {
            return;
        }

        Store.SelectElection(election);
        await Navigation.PushAsync(new ElectionDetailPage(Store));
    }

    private async void OnViewResultsClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(new ElectionResultsPage(Store, _electionTitle));

    private async void OnBackTapped(object? sender, TappedEventArgs e) =>
        await Navigation.PopAsync();
}
