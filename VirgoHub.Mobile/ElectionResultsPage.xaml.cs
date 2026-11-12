using System.Collections.ObjectModel;
using VirgoHub.Mobile.Services;
using VirgoHub.Shared;

namespace VirgoHub.Mobile;

public partial class ElectionResultsPage : ContentPage
{
    private readonly string _electionTitle;

    public ElectionResultsPage(StudentPortalStore store, string electionTitle)
    {
        InitializeComponent();
        Store = store;
        _electionTitle = electionTitle;
        BindingContext = this;
    }

    public StudentPortalStore Store { get; }

    public ObservableCollection<Election> ResultGroups { get; } = [];

    public string ElectionTitle => _electionTitle;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ResultGroups.Clear();
        foreach (var item in Store.ActiveElections.Where(x => string.Equals(x.Title, _electionTitle, StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.Position))
        {
            ResultGroups.Add(item);
        }
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e) =>
        await Navigation.PopAsync();
}
