using System.Runtime.CompilerServices;
using VirgoHub.Mobile.Services;
using VirgoHub.Shared;

namespace VirgoHub.Mobile;

public partial class SportsPage : ContentPage
{
    private SportsFacility? _selectedFacility;
    private DateTime _selectedBookingDate = DateTime.Today.AddDays(1);
    private string _requestedTime = string.Empty;
    private string _bookingPurpose = string.Empty;

    public SportsPage(StudentPortalStore store)
    {
        InitializeComponent();
        Store = store;
        BindingContext = this;
    }

    public StudentPortalStore Store { get; }

    public SportsFacility? SelectedFacility
    {
        get => _selectedFacility;
        set => SetProperty(ref _selectedFacility, value);
    }

    public DateTime SelectedBookingDate
    {
        get => _selectedBookingDate;
        set => SetProperty(ref _selectedBookingDate, value);
    }

    public string RequestedTime
    {
        get => _requestedTime;
        set => SetProperty(ref _requestedTime, value);
    }

    public string BookingPurpose
    {
        get => _bookingPurpose;
        set => SetProperty(ref _bookingPurpose, value);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        SelectedFacility ??= Store.Facilities.FirstOrDefault();
    }

    private async void OnReserveClicked(object? sender, EventArgs e)
    {
        try
        {
            if (SelectedFacility is null || string.IsNullOrWhiteSpace(RequestedTime))
            {
                throw new InvalidOperationException("Choose a facility and enter the time you want first.");
            }

            await Store.CreateBookingAsync(new CreateBookingRequest
            {
                FacilityId = SelectedFacility.Id,
                BookingDate = DateOnly.FromDateTime(SelectedBookingDate),
                SlotLabel = RequestedTime,
                Purpose = BookingPurpose
            });

            RequestedTime = string.Empty;
            BookingPurpose = string.Empty;
            await DisplayAlert("VirgoHub", "Your sports booking request has been sent for approval.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("VirgoHub", ex.Message, "OK");
        }
    }

    private async void OnApproveBookingClicked(object? sender, EventArgs e)
    {
        try
        {
            if (sender is not Button { BindingContext: SportsBooking booking })
            {
                return;
            }

            await Store.ApproveBookingAsync(booking.Id);
            await DisplayAlert("VirgoHub", "The booking request has been approved.", "OK");
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
