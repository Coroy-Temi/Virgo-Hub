using VirgoHub.Shared;

namespace VirgoHub.Mobile;

public sealed class ElectionGroupView
{
    public string Title { get; init; } = string.Empty;
    public List<Election> Positions { get; init; } = [];
    public int OpenPositions { get; init; }
    public int TotalVotes { get; init; }
}
