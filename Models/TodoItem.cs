using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace TodoWpfPortable.Models;

public sealed class TodoItem : INotifyPropertyChanged
{
    private string _title = string.Empty;
    private bool _isCompleted;
    private DateTimeOffset? _dueDate;
    private DateTimeOffset? _nextActivationDate;
    private string _repeatOption = "없음";
    private ObservableCollection<int> _weeklyDays = [];
    private string _monthlyRepeatMode = "날짜";
    private int _monthlyDay = 1;
    private int _monthlyWeek = 1;
    private int _monthlyDayOfWeek = 1;
    private string _note = string.Empty;
    private bool _isMemo;
    private bool _isDeleted;
    private bool _isEditingTitle;
    private bool _isStickyNoteOpen;
    private bool _isStickyNoteEnabled;
    private bool _isPinned;
    private bool _isAddingSubTask;
    private bool _isSubTasksExpanded = true;
    private string _subTaskDraft = string.Empty;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title
    {
        get => _title;
        set => SetField(ref _title, value);
    }

    public bool IsCompleted
    {
        get => _isCompleted;
        set
        {
            if (SetField(ref _isCompleted, value))
            {
                CompletedAt = value ? DateTimeOffset.Now : null;
                OnPropertyChanged(nameof(CompletedAt));
                NotifyDetailProperties();
            }
        }
    }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public bool IsMemo
    {
        get => _isMemo;
        set => SetField(ref _isMemo, value);
    }

    public bool IsDeleted
    {
        get => _isDeleted;
        set
        {
            if (SetField(ref _isDeleted, value))
            {
                OnPropertyChanged(nameof(HasTopStatus));
                OnPropertyChanged(nameof(StickyStatusText));
            }
        }
    }

    [JsonIgnore]
    public bool IsEditingTitle
    {
        get => _isEditingTitle;
        set => SetField(ref _isEditingTitle, value);
    }

    public bool IsStickyNoteEnabled
    {
        get => _isStickyNoteEnabled;
        set
        {
            if (SetField(ref _isStickyNoteEnabled, value))
            {
                OnPropertyChanged(nameof(HasTopStatus));
                OnPropertyChanged(nameof(StickyStatusText));
            }
        }
    }

    public double? StickyLeft { get; set; }

    public double? StickyTop { get; set; }

    public double? StickyWidth { get; set; }

    public double? StickyHeight { get; set; }
    [JsonIgnore]
    public bool IsStickyNoteOpen
    {
        get => _isStickyNoteOpen;
        set
        {
            if (SetField(ref _isStickyNoteOpen, value))
            {
                OnPropertyChanged(nameof(HasTopStatus));
                OnPropertyChanged(nameof(StickyStatusText));
            }
        }
    }

    [JsonIgnore]
    public bool HasTopStatus => !IsDeleted && (IsPinned || IsStickyNoteEnabled || IsStickyNoteOpen);

    [JsonIgnore]
    public string StickyStatusText => IsPinned && (IsStickyNoteEnabled || IsStickyNoteOpen)
        ? "고정됨  ·  표시중"
        : IsPinned
            ? "고정됨"
            : IsStickyNoteEnabled || IsStickyNoteOpen
                ? "표시중"
                : string.Empty;
    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset? DueDate
    {
        get => _dueDate;
        set
        {
            if (SetField(ref _dueDate, value))
            {
                OnPropertyChanged(nameof(DetailSummary));
                OnPropertyChanged(nameof(NonDueDetailSummary));
                OnPropertyChanged(nameof(NonDueDetailDisplay));
                OnPropertyChanged(nameof(DueDisplayText));
                OnPropertyChanged(nameof(HasDetails));
                OnPropertyChanged(nameof(IsDueToday));
                OnPropertyChanged(nameof(HasDueDate));
                OnPropertyChanged(nameof(RepeatReactivateDisplayText));
                OnPropertyChanged(nameof(RepeatReactivateSummary));
                OnPropertyChanged(nameof(IsWaitingForNextRepeat));
            }
        }
    }

    public DateTimeOffset? NextActivationDate
    {
        get => _nextActivationDate;
        set
        {
            if (SetField(ref _nextActivationDate, value))
            {
                NotifyDetailProperties();
                OnPropertyChanged(nameof(RepeatReactivateDisplayText));
                OnPropertyChanged(nameof(RepeatReactivateSummary));
                OnPropertyChanged(nameof(IsWaitingForNextRepeat));
            }
        }
    }

    public string RepeatOption
    {
        get => _repeatOption;
        set
        {
            if (SetField(ref _repeatOption, value))
            {
                NotifyDetailProperties();
            }
        }
    }

    public ObservableCollection<int> WeeklyDays
    {
        get => _weeklyDays;
        set
        {
            if (SetField(ref _weeklyDays, value))
            {
                NotifyDetailProperties();
            }
        }
    }

    public string MonthlyRepeatMode
    {
        get => _monthlyRepeatMode;
        set
        {
            if (SetField(ref _monthlyRepeatMode, value))
            {
                NotifyDetailProperties();
            }
        }
    }

    public int MonthlyDay
    {
        get => _monthlyDay;
        set
        {
            if (SetField(ref _monthlyDay, value))
            {
                NotifyDetailProperties();
            }
        }
    }

    public int MonthlyWeek
    {
        get => _monthlyWeek;
        set
        {
            if (SetField(ref _monthlyWeek, value))
            {
                NotifyDetailProperties();
            }
        }
    }

    public int MonthlyDayOfWeek
    {
        get => _monthlyDayOfWeek;
        set
        {
            if (SetField(ref _monthlyDayOfWeek, value))
            {
                NotifyDetailProperties();
            }
        }
    }

    public string Note
    {
        get => _note;
        set
        {
            if (SetField(ref _note, value))
            {
                OnPropertyChanged(nameof(DetailSummary));
                OnPropertyChanged(nameof(NonDueDetailSummary));
                OnPropertyChanged(nameof(NonDueDetailDisplay));
                OnPropertyChanged(nameof(HasDetails));
            }
        }
    }

    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            if (SetField(ref _isPinned, value))
            {
                OnPropertyChanged(nameof(DetailSummary));
                OnPropertyChanged(nameof(NonDueDetailSummary));
                OnPropertyChanged(nameof(NonDueDetailDisplay));
                OnPropertyChanged(nameof(HasDetails));
            }
        }
    }

    public ObservableCollection<TodoItem> SubTasks { get; set; } = [];

    [JsonIgnore]
    public bool IsAddingSubTask
    {
        get => _isAddingSubTask;
        set => SetField(ref _isAddingSubTask, value);
    }

    [JsonIgnore]
    public string SubTaskDraft
    {
        get => _subTaskDraft;
        set => SetField(ref _subTaskDraft, value);
    }

    [JsonIgnore]
    public bool IsSubTasksExpanded
    {
        get => _isSubTasksExpanded;
        set => SetField(ref _isSubTasksExpanded, value);
    }

    [JsonIgnore]
    public bool HasDetails =>
        DueDate is not null ||
        !string.IsNullOrWhiteSpace(Note) ||
        !string.IsNullOrWhiteSpace(RepeatOption) && RepeatOption != "없음";

    [JsonIgnore]
    public string DetailSummary
    {
        get
        {
            var details = new List<string>();

            if (HasDueDate)
            {
                details.Add(DueDisplayText);
            }

            var nonDueDetails = NonDueDetailSummary;
            if (!string.IsNullOrWhiteSpace(nonDueDetails))
            {
                details.Add(nonDueDetails);
            }

            return string.Join("  ·  ", details);
        }
    }

    [JsonIgnore]
    public string NonDueDetailSummary
    {
        get
        {
            var details = new List<string>();

            if (!string.IsNullOrWhiteSpace(RepeatOption) && RepeatOption != "없음")
            {
                details.Add(RepeatDisplayText);
            }

            if (!string.IsNullOrWhiteSpace(Note))
            {
                details.Add("메모");
            }

            return string.Join("  ·  ", details);
        }
    }

    [JsonIgnore]
    public string NonDueDetailDisplay
    {
        get
        {
            var summary = NonDueDetailSummary;
            if (string.IsNullOrWhiteSpace(summary))
            {
                return string.Empty;
            }

            return HasDueDate ? $"  ·  {summary}" : summary;
        }
    }

    [JsonIgnore]
    public bool HasNonDueDetails => !string.IsNullOrWhiteSpace(NonDueDetailSummary);

    [JsonIgnore]
    public bool IsDueToday => DueDate?.Date == DateTimeOffset.Now.Date;

    [JsonIgnore]
    public bool HasDueDate => DueDate is not null;

    [JsonIgnore]
    public string DueDisplayText
    {
        get
        {
            if (DueDate is not { } dueDate)
            {
                return string.Empty;
            }

            var dueDay = dueDate.Date;
            var today = DateTimeOffset.Now.Date;
            return dueDay == today
                ? "오늘까지"
                : dueDay == today.AddDays(1)
                    ? "내일까지"
                    : $"{dueDay.Month}월 {dueDay.Day}일까지";
        }
    }

    [JsonIgnore]
    public string RepeatReactivateDisplayText
    {
        get
        {
            if (NextActivationDate is not { } dueDate)
            {
                return string.Empty;
            }

            var dueDay = dueDate.Date;
            var today = DateTimeOffset.Now.Date;
            return dueDay == today
                ? "오늘"
                : dueDay == today.AddDays(1)
                    ? "내일"
                    : $"{dueDay.Month}월 {dueDay.Day}일";
        }
    }

    [JsonIgnore]
    public bool IsWaitingForNextRepeat => RepeatOption != "없음" && NextActivationDate is not null;

    [JsonIgnore]
    public string RepeatDisplayText
    {
        get
        {
            var displayText = RepeatOption switch
            {
                "매주" => WeeklyDays.Count > 0
                    ? $"반복 매주({string.Join(", ", WeeklyDays.OrderBy(day => day).Select(ToDayLabel))})"
                    : "반복 매주",
                "매월" when MonthlyRepeatMode == "주차요일" =>
                    $"반복 매월({MonthlyWeek}주차 {ToDayLabel(MonthlyDayOfWeek)}요일)",
                "매월" => $"반복 매월({MonthlyDay}일)",
                _ => $"반복 {RepeatOption}"
            };

            return IsWaitingForNextRepeat
                ? $"{displayText}{RepeatReactivateSummary}"
                : displayText;
        }
    }

    public static string ToDayLabel(int dayOfWeek)
    {
        return dayOfWeek switch
        {
            0 => "일",
            1 => "월",
            2 => "화",
            3 => "수",
            4 => "목",
            5 => "금",
            6 => "토",
            _ => "월"
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(string? propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void NotifyDetailProperties()
    {
        OnPropertyChanged(nameof(DetailSummary));
        OnPropertyChanged(nameof(NonDueDetailSummary));
        OnPropertyChanged(nameof(NonDueDetailDisplay));
        OnPropertyChanged(nameof(HasNonDueDetails));
        OnPropertyChanged(nameof(RepeatDisplayText));
        OnPropertyChanged(nameof(RepeatReactivateDisplayText));
        OnPropertyChanged(nameof(RepeatReactivateSummary));
        OnPropertyChanged(nameof(IsWaitingForNextRepeat));
        OnPropertyChanged(nameof(HasDetails));
    }

    [JsonIgnore]
    public string RepeatReactivateSummary
    {
        get
        {
            if (!IsWaitingForNextRepeat)
            {
                return string.Empty;
            }

            return $" · {RepeatReactivateDisplayText} 다시 표시";
        }
    }
}
