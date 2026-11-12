using System.Runtime.CompilerServices;
using VirgoHub.Mobile.Services;
using VirgoHub.Shared;

namespace VirgoHub.Mobile;

public partial class ComplaintsPage : ContentPage
{
    private string _complaintTitle = string.Empty;
    private string? _selectedCategory;
    private string _hostelName = string.Empty;
    private string _roomNumber = string.Empty;
    private string _complaintDescription = string.Empty;

    public ComplaintsPage(StudentPortalStore store)
    {
        InitializeComponent();
        Store = store;
        _selectedCategory = Store.ComplaintCategories.FirstOrDefault();
        _hostelName = Store.CurrentUser?.Hostel ?? string.Empty;
        BindingContext = this;
    }

    public StudentPortalStore Store { get; }

    public string ComplaintTitle
    {
        get => _complaintTitle;
        set => SetProperty(ref _complaintTitle, value);
    }

    public string? SelectedCategory
    {
        get => _selectedCategory;
        set => SetProperty(ref _selectedCategory, value);
    }

    public string HostelName
    {
        get => _hostelName;
        set => SetProperty(ref _hostelName, value);
    }

    public string RoomNumber
    {
        get => _roomNumber;
        set => SetProperty(ref _roomNumber, value);
    }

    public string ComplaintDescription
    {
        get => _complaintDescription;
        set => SetProperty(ref _complaintDescription, value);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        SelectedCategory ??= Store.ComplaintCategories.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(HostelName))
        {
            HostelName = Store.CurrentUser?.Hostel ?? string.Empty;
        }
    }

    private async void OnSendComplaintClicked(object? sender, EventArgs e)
    {
        try
        {
            await Store.CreateComplaintAsync(new CreateComplaintRequest
            {
                Title = ComplaintTitle,
                Category = SelectedCategory ?? string.Empty,
                Hostel = HostelName,
                Room = RoomNumber,
                Description = ComplaintDescription
            });

            ComplaintTitle = string.Empty;
            ComplaintDescription = string.Empty;
            RoomNumber = string.Empty;
            await DisplayAlert("VirgoHub", "Your complaint has been sent to hostel support.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("VirgoHub", ex.Message, "OK");
        }
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
