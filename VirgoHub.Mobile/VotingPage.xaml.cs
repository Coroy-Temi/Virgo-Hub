using System.Collections.ObjectModel;
using VirgoHub.Mobile.Services;
using VirgoHub.Shared;

namespace VirgoHub.Mobile;

public partial class VotingPage : ContentPage
{
    public VotingPage(StudentPortalStore store)
    {
        InitializeComponent();
        Store = store;
        BindingContext = this;
    }

    public StudentPortalStore Store { get; }

    public ObservableCollection<ElectionGroupView> VisibleElectionGroups { get; } = [];

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RefreshGroups();
    }

    private async void OnOpenElectionClicked(object? sender, EventArgs e)
    {
        try
        {
            if (sender is not Button { BindingContext: ElectionGroupView group })
            {
                return;
            }

            await Navigation.PushAsync(new ElectionGroupPage(Store, group.Title));
        }
        catch (Exception ex)
        {
            await DisplayAlert("VirgoHub", ex.Message, "OK");
        }
    }

    private void RefreshGroups()
    {
        VisibleElectionGroups.Clear();

        foreach (var group in Store.ActiveElections
                     .GroupBy(x => x.Title)
                     .OrderBy(x => x.Key)
                     .Select(group => new ElectionGroupView
                     {
                         Title = group.Key,
                         Positions = group.OrderBy(x => x.Position).ToList(),
                         OpenPositions = group.Count(x => x.IsOpen),
                         TotalVotes = group.Sum(x => x.TotalVotes)
                     }))
        {
            VisibleElectionGroups.Add(group);
        }
    }
}
