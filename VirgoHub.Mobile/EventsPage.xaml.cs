using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using VirgoHub.Mobile.Services;
using VirgoHub.Shared;

namespace VirgoHub.Mobile;

public partial class EventsPage : ContentPage
{
    private PersonalTeachingGroup? _selectedGroup;
    private string _assessmentTitle = string.Empty;
    private string _assessmentSummary = string.Empty;
    private string _timeLimitMinutes = "20";
    private PersonalAssessmentType _selectedAssessmentType = PersonalAssessmentType.Test;

    public EventsPage(StudentPortalStore store)
    {
        InitializeComponent();
        Store = store;
        BindingContext = this;
        ResetQuestionDrafts();
    }

    public StudentPortalStore Store { get; }

    public ObservableCollection<AssessmentQuestionDraft> QuestionDrafts { get; } = [];

    public PersonalTeachingGroup? SelectedGroup
    {
        get => _selectedGroup;
        set => SetProperty(ref _selectedGroup, value);
    }

    public string AssessmentTitle
    {
        get => _assessmentTitle;
        set => SetProperty(ref _assessmentTitle, value);
    }

    public string AssessmentSummary
    {
        get => _assessmentSummary;
        set => SetProperty(ref _assessmentSummary, value);
    }

    public string TimeLimitMinutes
    {
        get => _timeLimitMinutes;
        set => SetProperty(ref _timeLimitMinutes, value);
    }

    public PersonalAssessmentType SelectedAssessmentType
    {
        get => _selectedAssessmentType;
        set => SetProperty(ref _selectedAssessmentType, value);
    }

    public IReadOnlyList<PersonalAssessmentType> AssessmentTypeOptions { get; } =
    [
        PersonalAssessmentType.Test,
        PersonalAssessmentType.Exam
    ];

    public string HeaderIcon => Store.IsPersonalMode ? "icon_results.svg" : "icon_events.svg";

    public string HeaderTitle => Store.IsPersonalMode ? "Plans & Assessments" : "Events";

    public string HeaderSummary => Store.IsPersonalMode
        ? "Manage reminders, publish tests or exams, and keep each teaching group academically on track."
        : "Stay close to chapel notices, school updates, and upcoming campus moments.";

    public string ReminderSectionTitle => Store.IsPersonalMode ? "Study Reminders" : "Upcoming Events";

    public string AssessmentSectionTitle => Store.IsPersonalMode ? "Assessment Board" : "Event Board";

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (Store.IsAuthenticated)
        {
            await Store.RefreshAsync();
            OnPropertyChanged(nameof(HeaderIcon));
            OnPropertyChanged(nameof(HeaderTitle));
            OnPropertyChanged(nameof(HeaderSummary));
            OnPropertyChanged(nameof(ReminderSectionTitle));
            OnPropertyChanged(nameof(AssessmentSectionTitle));
        }
    }

    private void OnAddQuestionClicked(object? sender, EventArgs e)
    {
        QuestionDrafts.Add(new AssessmentQuestionDraft(QuestionDrafts.Count + 1));
    }

    private void OnRemoveQuestionClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: AssessmentQuestionDraft draft } && QuestionDrafts.Count > 1)
        {
            QuestionDrafts.Remove(draft);
            RenumberQuestions();
        }
    }

    private async void OnCreateAssessmentClicked(object? sender, EventArgs e)
    {
        try
        {
            if (!int.TryParse(TimeLimitMinutes, out var timeLimit))
            {
                throw new InvalidOperationException("Enter a valid time limit in minutes.");
            }

            await Store.CreatePersonalAssessmentAsync(new CreatePersonalAssessmentRequest
            {
                GroupId = SelectedGroup?.Id ?? string.Empty,
                Title = AssessmentTitle,
                Summary = AssessmentSummary,
                Type = SelectedAssessmentType,
                TimeLimitMinutes = timeLimit,
                OpensAt = DateTimeOffset.UtcNow,
                DueAt = DateTimeOffset.UtcNow.AddDays(7),
                Questions = QuestionDrafts.Select(x => new CreatePersonalAssessmentQuestionRequest
                {
                    Prompt = x.Prompt,
                    OptionA = x.OptionA,
                    OptionB = x.OptionB,
                    OptionC = x.OptionC,
                    OptionD = x.OptionD,
                    CorrectOption = x.CorrectOption,
                    Explanation = x.Explanation
                }).ToList()
            });

            SelectedGroup = null;
            AssessmentTitle = string.Empty;
            AssessmentSummary = string.Empty;
            TimeLimitMinutes = "20";
            SelectedAssessmentType = PersonalAssessmentType.Test;
            ResetQuestionDrafts();
            await DisplayAlert("VirgoHub", "Assessment published successfully.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("VirgoHub", ex.Message, "OK");
        }
    }

    private async void OnOpenAssessmentClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: PersonalAssessment assessment })
        {
            return;
        }

        Store.SelectPersonalAssessment(assessment);
        await Navigation.PushAsync(new PersonalAssessmentDetailPage(Store));
    }

    private async void OnRefreshClicked(object? sender, EventArgs e)
    {
        await Store.RefreshAsync();
    }

    private void ResetQuestionDrafts()
    {
        QuestionDrafts.Clear();
        QuestionDrafts.Add(new AssessmentQuestionDraft(1));
        QuestionDrafts.Add(new AssessmentQuestionDraft(2));
    }

    private void RenumberQuestions()
    {
        for (var index = 0; index < QuestionDrafts.Count; index++)
        {
            QuestionDrafts[index].Number = index + 1;
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

    public sealed class AssessmentQuestionDraft : BindableObject
    {
        private int _number;
        private string _prompt = string.Empty;
        private string _optionA = string.Empty;
        private string _optionB = string.Empty;
        private string _optionC = string.Empty;
        private string _optionD = string.Empty;
        private string _correctOption = "A";
        private string _explanation = string.Empty;

        public AssessmentQuestionDraft(int number)
        {
            _number = number;
        }

        public IReadOnlyList<string> CorrectOptionChoices { get; } = ["A", "B", "C", "D"];

        public int Number
        {
            get => _number;
            set
            {
                if (_number == value)
                {
                    return;
                }

                _number = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Heading));
            }
        }

        public string Heading => $"Question {Number}";

        public string Prompt
        {
            get => _prompt;
            set => SetField(ref _prompt, value);
        }

        public string OptionA
        {
            get => _optionA;
            set => SetField(ref _optionA, value);
        }

        public string OptionB
        {
            get => _optionB;
            set => SetField(ref _optionB, value);
        }

        public string OptionC
        {
            get => _optionC;
            set => SetField(ref _optionC, value);
        }

        public string OptionD
        {
            get => _optionD;
            set => SetField(ref _optionD, value);
        }

        public string CorrectOption
        {
            get => _correctOption;
            set => SetField(ref _correctOption, value);
        }

        public string Explanation
        {
            get => _explanation;
            set => SetField(ref _explanation, value);
        }

        private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            OnPropertyChanged(propertyName);
        }
    }
}
