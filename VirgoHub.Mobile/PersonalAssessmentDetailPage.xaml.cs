using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using VirgoHub.Mobile.Services;
using VirgoHub.Shared;

namespace VirgoHub.Mobile;

public partial class PersonalAssessmentDetailPage : ContentPage
{
    private PersonalAssessmentSubmission? _reviewedSubmission;

    public PersonalAssessmentDetailPage(StudentPortalStore store)
    {
        InitializeComponent();
        Store = store;
        BindingContext = this;
    }

    public StudentPortalStore Store { get; }

    public ObservableCollection<AssessmentQuestionViewModel> Questions { get; } = [];
    public ObservableCollection<SubmissionReviewCardViewModel> SubmissionCards { get; } = [];

    public bool IsLecturerView => Store.IsPersonalLecturer;

    public bool HasSubmission => Store.SelectedPersonalAssessmentSubmission is not null;

    public bool HasReviewedSubmission => ReviewedSubmission is not null;

    public bool CanSubmit => Store.IsPersonalLearner && !HasSubmission && Questions.Count > 0;

    public PersonalAssessmentSubmission? ReviewedSubmission
    {
        get => _reviewedSubmission;
        private set
        {
            if (_reviewedSubmission == value)
            {
                return;
            }

            _reviewedSubmission = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasReviewedSubmission));
            OnPropertyChanged(nameof(ReviewedSubmissionHeadline));
            OnPropertyChanged(nameof(ReviewedSubmissionMeta));
            OnPropertyChanged(nameof(StatusSummary));
        }
    }

    public string StatusSummary
    {
        get
        {
            var assessment = Store.SelectedPersonalAssessment;
            if (assessment is null)
            {
                return "No assessment selected.";
            }

            if (Store.IsPersonalLecturer)
            {
                return HasReviewedSubmission
                    ? $"Reviewing {ReviewedSubmission!.StudentName}'s submission."
                    : "No learner submission selected yet.";
            }

            if (HasSubmission)
            {
                return SubmissionSummary;
            }

            return $"{assessment.Type} closes on {assessment.DueAt:dd MMM yyyy, hh:mm tt}. Results unlock after you submit.";
        }
    }

    public string SubmissionSummary
    {
        get
        {
            var submission = Store.SelectedPersonalAssessmentSubmission;
            return submission is null
                ? "No submission recorded yet."
                : $"Submitted with a score of {submission.Score}/{submission.TotalQuestions} on {submission.SubmittedAt:dd MMM yyyy, hh:mm tt}.";
        }
    }

    public string LecturerSummary
    {
        get
        {
            var assessmentId = Store.SelectedPersonalAssessment?.Id;
            var submissions = Store.GetSubmissionsForAssessment(assessmentId);
            return submissions.Count == 0
                ? "No learner submissions yet."
                : $"{submissions.Count} learner submission(s) recorded so far. Open one below to inspect the answers and score.";
        }
    }

    public string ReviewedSubmissionHeadline => ReviewedSubmission is null
        ? "Submission review"
        : $"{ReviewedSubmission.StudentName} scored {ReviewedSubmission.Score}/{ReviewedSubmission.TotalQuestions}";

    public string ReviewedSubmissionMeta
    {
        get
        {
            if (ReviewedSubmission is null)
            {
                return "Pick a learner submission to review the selected answers and score.";
            }

            var percentage = ReviewedSubmission.TotalQuestions == 0
                ? 0
                : Math.Round((double)ReviewedSubmission.Score / ReviewedSubmission.TotalQuestions * 100, MidpointRounding.AwayFromZero);

            return $"Submitted on {ReviewedSubmission.SubmittedAt:dd MMM yyyy, hh:mm tt} | {percentage}%";
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        BuildQuestionView();
    }

    private async void OnSubmitClicked(object? sender, EventArgs e)
    {
        try
        {
            if (Questions.Any(x => string.IsNullOrWhiteSpace(x.SelectedOption)))
            {
                throw new InvalidOperationException("Answer every question before submitting the assessment.");
            }

            await Store.SubmitPersonalAssessmentAsync(new SubmitPersonalAssessmentRequest
            {
                AssessmentId = Store.SelectedPersonalAssessment?.Id ?? string.Empty,
                Responses = Questions.Select(x => new SubmitPersonalAssessmentResponseRequest
                {
                    QuestionId = x.QuestionId,
                    SelectedOption = x.SelectedOption
                }).ToList()
            });

            BuildQuestionView();
            await DisplayAlert("VirgoHub", "Assessment submitted successfully.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("VirgoHub", ex.Message, "OK");
        }
    }

    private void BuildQuestionView()
    {
        Questions.Clear();
        SubmissionCards.Clear();
        var assessment = Store.SelectedPersonalAssessment;
        if (assessment is null)
        {
            return;
        }

        var availableSubmissions = Store.GetSubmissionsForAssessment(assessment.Id);
        foreach (var submission in availableSubmissions)
        {
            SubmissionCards.Add(new SubmissionReviewCardViewModel(submission));
        }

        if (Store.IsPersonalLecturer)
        {
            ReviewedSubmission = availableSubmissions.FirstOrDefault(x =>
                                   x.Id.Equals(ReviewedSubmission?.Id ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                               ?? availableSubmissions.FirstOrDefault();
        }
        else
        {
            ReviewedSubmission = Store.SelectedPersonalAssessmentSubmission;
        }

        var submissionToReview = ReviewedSubmission;
        for (var index = 0; index < assessment.Questions.Count; index++)
        {
            var question = assessment.Questions[index];
            var selectedOption = submissionToReview?.Responses
                .FirstOrDefault(x => x.QuestionId.Equals(question.Id, StringComparison.OrdinalIgnoreCase))
                ?.SelectedOption ?? string.Empty;

            Questions.Add(new AssessmentQuestionViewModel
            {
                Number = index + 1,
                QuestionId = question.Id,
                Prompt = question.Prompt,
                Options =
                [
                    $"A. {question.OptionA}",
                    $"B. {question.OptionB}",
                    $"C. {question.OptionC}",
                    $"D. {question.OptionD}"
                ],
                SelectedOption = selectedOption,
                CorrectOption = question.CorrectOption,
                Explanation = string.IsNullOrWhiteSpace(question.Explanation)
                    ? "No explanation was added for this question."
                    : question.Explanation,
                CanAnswer = Store.IsPersonalLearner && submissionToReview is null,
                ShowReview = submissionToReview is not null || Store.IsPersonalLecturer
            });
        }

        OnPropertyChanged(nameof(IsLecturerView));
        OnPropertyChanged(nameof(HasSubmission));
        OnPropertyChanged(nameof(HasReviewedSubmission));
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(StatusSummary));
        OnPropertyChanged(nameof(SubmissionSummary));
        OnPropertyChanged(nameof(LecturerSummary));
        OnPropertyChanged(nameof(ReviewedSubmissionHeadline));
        OnPropertyChanged(nameof(ReviewedSubmissionMeta));
    }

    private void OnReviewSubmissionClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: SubmissionReviewCardViewModel card })
        {
            return;
        }

        ReviewedSubmission = card.Submission;
        var assessment = Store.SelectedPersonalAssessment;
        if (assessment is null)
        {
            return;
        }

        Questions.Clear();
        for (var index = 0; index < assessment.Questions.Count; index++)
        {
            var question = assessment.Questions[index];
            var selectedOption = ReviewedSubmission?.Responses
                .FirstOrDefault(x => x.QuestionId.Equals(question.Id, StringComparison.OrdinalIgnoreCase))
                ?.SelectedOption ?? string.Empty;

            Questions.Add(new AssessmentQuestionViewModel
            {
                Number = index + 1,
                QuestionId = question.Id,
                Prompt = question.Prompt,
                Options =
                [
                    $"A. {question.OptionA}",
                    $"B. {question.OptionB}",
                    $"C. {question.OptionC}",
                    $"D. {question.OptionD}"
                ],
                SelectedOption = selectedOption,
                CorrectOption = question.CorrectOption,
                Explanation = string.IsNullOrWhiteSpace(question.Explanation)
                    ? "No explanation was added for this question."
                    : question.Explanation,
                CanAnswer = false,
                ShowReview = true
            });
        }

        OnPropertyChanged(nameof(HasReviewedSubmission));
        OnPropertyChanged(nameof(StatusSummary));
        OnPropertyChanged(nameof(ReviewedSubmissionHeadline));
        OnPropertyChanged(nameof(ReviewedSubmissionMeta));
    }

    public sealed class AssessmentQuestionViewModel : BindableObject
    {
        private string _selectedOption = string.Empty;

        public int Number { get; set; }
        public string QuestionId { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        public List<string> Options { get; set; } = [];
        public IReadOnlyList<string> OptionKeys { get; } = ["A", "B", "C", "D"];
        public string CorrectOption { get; set; } = "A";
        public string Explanation { get; set; } = string.Empty;
        public bool CanAnswer { get; set; }
        public bool ShowReview { get; set; }

        public string Heading => $"Question {Number}";

        public string SelectedOption
        {
            get => _selectedOption;
            set
            {
                if (SetProperty(ref _selectedOption, value))
                {
                    OnPropertyChanged(nameof(ReviewAnswer));
                    OnPropertyChanged(nameof(HasAnswer));
                }
            }
        }

        public bool HasAnswer => !string.IsNullOrWhiteSpace(SelectedOption);

        public string ReviewAnswer => string.IsNullOrWhiteSpace(SelectedOption)
            ? "No answer submitted."
            : $"Your answer: {SelectedOption}";

        public string CorrectAnswer => $"Correct answer: {CorrectOption}";

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

    public sealed class SubmissionReviewCardViewModel
    {
        public SubmissionReviewCardViewModel(PersonalAssessmentSubmission submission)
        {
            Submission = submission;
        }

        public PersonalAssessmentSubmission Submission { get; }

        public string StudentName => Submission.StudentName;

        public string ScoreLabel => $"{Submission.Score}/{Submission.TotalQuestions}";

        public string Meta
        {
            get
            {
                var percentage = Submission.TotalQuestions == 0
                    ? 0
                    : Math.Round((double)Submission.Score / Submission.TotalQuestions * 100, MidpointRounding.AwayFromZero);

                return $"{percentage}% | {Submission.SubmittedAt:dd MMM yyyy, hh:mm tt}";
            }
        }
    }
}
