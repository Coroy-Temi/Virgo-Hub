using System.Collections.ObjectModel;
using VirgoHub.Mobile.Services;
using VirgoHub.Shared;

namespace VirgoHub.Mobile;

public partial class ElectionDetailPage : ContentPage
{
    public ElectionDetailPage(StudentPortalStore store)
    {
        InitializeComponent();
        Store = store;
        BindingContext = this;
    }

    public StudentPortalStore Store { get; }

    public ObservableCollection<Candidate> VisibleCandidates { get; } = [];

    public string VoteButtonText => Store.SelectedElection?.HasVoted == true
        ? "Vote recorded"
        : Store.SelectedElection?.IsOpen == true
            ? "Vote now"
            : "Voting closed";

    public string ElectionStatus => Store.SelectedElection switch
    {
        null => "No election selected",
        { IsOpen: true } => "Voting is open",
        { IsClosed: true } => "Voting closed",
        _ => "Scheduled"
    };

    public string ElectionWindow => Store.SelectedElection is null
        ? "No window set"
        : $"{Store.SelectedElection.OpensAt.LocalDateTime:dd MMM} - {Store.SelectedElection.ClosesAt.LocalDateTime:dd MMM}";

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RefreshVisibleCandidates();
        OnPropertyChanged(nameof(VoteButtonText));
        OnPropertyChanged(nameof(ElectionStatus));
        OnPropertyChanged(nameof(ElectionWindow));
    }

    private async void OnVoteClicked(object? sender, EventArgs e)
    {
        try
        {
            if (sender is not Button { BindingContext: Candidate candidate })
            {
                return;
            }

            var election = Store.SelectedElection;
            if (election is null)
            {
                throw new InvalidOperationException("The selected election could not be found.");
            }

            if (!election.IsOpen)
            {
                throw new InvalidOperationException("This election is not open for voting.");
            }

            if (election.HasVoted)
            {
                throw new InvalidOperationException("Your vote has already been recorded for this election.");
            }

            await Store.CastVoteAsync(new CastVoteRequest
            {
                ElectionId = election.Id,
                CandidateId = candidate.Id
            });

            Store.SelectElection(Store.ActiveElections.FirstOrDefault(x => x.Id == election.Id));
            RefreshVisibleCandidates();
            OnPropertyChanged(nameof(VoteButtonText));
            OnPropertyChanged(nameof(ElectionStatus));
            OnPropertyChanged(nameof(ElectionWindow));
            await DisplayAlert("VirgoHub", $"Your vote for {candidate.Name} has been recorded successfully.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("VirgoHub", ex.Message, "OK");
        }
    }

    private void RefreshVisibleCandidates()
    {
        VisibleCandidates.Clear();

        foreach (var candidate in Store.SelectedElection?.Candidates ?? Enumerable.Empty<Candidate>())
        {
            VisibleCandidates.Add(candidate);
        }
    }
}
