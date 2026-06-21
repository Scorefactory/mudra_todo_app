using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using System.Reflection;
using System.Runtime.InteropServices;
using TodoWpfPortable.Models;
using TodoWpfPortable.Services;

namespace TodoWpfPortable;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly TodoStorageService _storage = new();
    private readonly AppSettingsService _settings = new();
    private readonly DispatcherTimer _saveTimer;
    private readonly DispatcherTimer _loadingAnimationTimer;
    private readonly Dictionary<Guid, Window> _stickyNotes = [];
    private Rect? _rightDockRestoreBounds;
    private double _todoFontSize = 13;
    private double _subTodoFontSize = 13;
    private double _memoFontSize = 13;
    private double _stickyNoteFontSize = 13;
    private double _addButtonSize = 16;
    private double _rowHeight = 30;
    private double _subRowHeight = 26;
    private Thickness _mainItemPadding = new(0, 3, 0, 3);
    private Thickness _subItemMargin = new(0);
    private string _currentTheme = "기본";
    private Point _dragStartPoint;
    private TodoItem? _draggedTodo;
    private ListBoxItem? _draggedContainer;
    private TodoItem? _draggedSubTodo;
    private TodoItem? _draggedSubParent;
    private ContentPresenter? _draggedSubContainer;
    private Window? _dragPreview;
    private string _activeFilter = "Open";
    private bool _isLoading;
    private bool _isApplyingSettings;
    private bool _isSchedulingRecurringTodo;
    private bool _showMemoTrash;
    private bool _isClosing;
    private bool _stickyNoteTopmost = true;
    private bool _autoStickyNewMemos = true;
    private int _loadingFrame;
    private ScaleTransform? _loadingTongueScale;
    private UIElement? _loadingTongueTip;
    private TranslateTransform? _loadingSpriteTransform;
    private TranslateTransform? _loadingEyeTransform;
    private ScaleTransform? _loadingShadowScale;
    private ScaleTransform? _settingsTongueScale;
    private UIElement? _settingsTongueTip;
    private TranslateTransform? _settingsSpriteTransform;
    private TranslateTransform? _settingsEyeTransform;
    private ScaleTransform? _settingsShadowScale;
    private string _memoSearchText = string.Empty;
    private string _stickyArrangeDirection = "Vertical";

    public MainWindow()
    {
        InitializeComponent();
        Resources[SystemParameters.VerticalScrollBarWidthKey] = 3.0;
        Resources[SystemParameters.HorizontalScrollBarHeightKey] = 3.0;
        BuildLoadingSprite(LoadingSpriteCanvas);
        BuildLoadingSprite(SettingsSpriteCanvas);
        LoadingVersionText.Text = $"v{GetAppVersion()}";
        SettingsVersionText.Text = $"v{GetAppVersion()}";

        Todos.CollectionChanged += Todos_CollectionChanged;
        DataContext = this;
        CollectionViewSource.GetDefaultView(Todos).Filter = FilterTodo;

        _saveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(650)
        };
        _saveTimer.Tick += SaveTimer_Tick;

        _loadingAnimationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(613)
        };
        _loadingAnimationTimer.Tick += LoadingAnimationTimer_Tick;
        _loadingAnimationTimer.Start();

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        SourceInitialized += (_, _) => ApplyTitleBarTheme(this);
    }

    public ObservableCollection<TodoItem> Todos { get; } = [];

    public double TodoFontSize
    {
        get => _todoFontSize;
        private set => SetProperty(ref _todoFontSize, value);
    }

    public double SubTodoFontSize
    {
        get => _subTodoFontSize;
        private set => SetProperty(ref _subTodoFontSize, value);
    }

    public double MemoFontSize
    {
        get => _memoFontSize;
        private set => SetProperty(ref _memoFontSize, value);
    }

    public double StickyNoteFontSize
    {
        get => _stickyNoteFontSize;
        private set => SetProperty(ref _stickyNoteFontSize, value);
    }

    public double AddButtonSize
    {
        get => _addButtonSize;
        private set => SetProperty(ref _addButtonSize, value);
    }

    public double RowHeight
    {
        get => _rowHeight;
        private set => SetProperty(ref _rowHeight, value);
    }

    public double SubRowHeight
    {
        get => _subRowHeight;
        private set => SetProperty(ref _subRowHeight, value);
    }

    public Thickness MainItemPadding
    {
        get => _mainItemPadding;
        private set => SetProperty(ref _mainItemPadding, value);
    }

    public Thickness SubItemMargin
    {
        get => _subItemMargin;
        private set => SetProperty(ref _subItemMargin, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var minimumLoading = Task.Delay(TimeSpan.FromSeconds(3));

        LoadAppSettings();

        if (RightDockToggle.IsChecked == true)
        {
            DockToRight();
        }

        await Task.WhenAll(LoadTodosAsync(), minimumLoading);
        HideLoadingOverlay();
        RestoreStickyNotes();
    }

    private static string GetAppVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is null ? "0.1.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    private void HideLoadingOverlay()
    {
        LoadingOverlay.Visibility = Visibility.Collapsed;
    }

    private void LoadingAnimationTimer_Tick(object? sender, EventArgs e)
    {
        _loadingFrame = (_loadingFrame + 1) % 4;

        var dots = (_loadingFrame % 3) + 1;
        LoadingStatusText.Text = $"로딩중{new string('.', dots)}";

        var tongueScale = _loadingFrame switch
        {
            0 => 0,
            1 => 0.65,
            2 => 1,
            _ => 0.35
        };

        if (_loadingTongueScale is not null)
        {
            _loadingTongueScale.ScaleX = tongueScale;
        }

        if (_settingsTongueScale is not null)
        {
            _settingsTongueScale.ScaleX = tongueScale;
        }

        if (_loadingTongueTip is not null)
        {
            _loadingTongueTip.Opacity = tongueScale >= 1 ? 1 : 0;
        }

        if (_settingsTongueTip is not null)
        {
            _settingsTongueTip.Opacity = tongueScale >= 1 ? 1 : 0;
        }

        var lifted = _loadingFrame is 1 or 2;
        if (_loadingSpriteTransform is not null)
        {
            _loadingSpriteTransform.Y = lifted ? -7 : 0;
        }

        if (_settingsSpriteTransform is not null)
        {
            _settingsSpriteTransform.Y = lifted ? -7 : 0;
        }

        if (_loadingEyeTransform is not null)
        {
            _loadingEyeTransform.X = lifted ? 7 : 0;
        }

        if (_settingsEyeTransform is not null)
        {
            _settingsEyeTransform.X = lifted ? 7 : 0;
        }

        if (_loadingShadowScale is not null)
        {
            _loadingShadowScale.ScaleX = lifted ? 0.86 : 1;
        }

        if (_settingsShadowScale is not null)
        {
            _settingsShadowScale.ScaleX = lifted ? 0.86 : 1;
        }
    }

    private void BuildLoadingSprite(Canvas targetCanvas)
    {
        const int pixel = 7;
        const string screen = "#101114";
        const string green1 = "#76E060";
        const string green2 = "#3BAE48";
        const string green3 = "#1F6B32";
        const string lime = "#C6F66F";
        const string pink1 = "#FF7AA8";
        const string pink2 = "#D53F73";
        const string white = "#F4F4D7";
        const string black = "#151515";
        const string wood1 = "#8A5A2C";
        const string wood2 = "#4B2D1C";

        targetCanvas.Children.Clear();

        AddRect(targetCanvas, 112, 118, 126, 8, wood1);
        AddRect(targetCanvas, 112, 123, 126, 5, wood2);
        AddRect(targetCanvas, 136, 113, 28, 5, wood2);
        AddRect(targetCanvas, 182, 123, 70, 5, wood2);

        var shadow = AddRect(targetCanvas, 110, 135, 161, 7, "#000000", 0.34);
        var shadowScale = new ScaleTransform(1, 1);
        if (ReferenceEquals(targetCanvas, LoadingSpriteCanvas))
        {
            _loadingShadowScale = shadowScale;
        }
        else if (ReferenceEquals(targetCanvas, SettingsSpriteCanvas))
        {
            _settingsShadowScale = shadowScale;
        }
        shadow.RenderTransformOrigin = new Point(0.5, 0.5);
        shadow.RenderTransform = shadowScale;

        var tongue = new Canvas
        {
            Width = 126,
            Height = 21,
            RenderTransformOrigin = new Point(0, 0.5),
            RenderTransform = new ScaleTransform(0, 1)
        };
        if (ReferenceEquals(targetCanvas, LoadingSpriteCanvas) && tongue.RenderTransform is ScaleTransform tongueScale)
        {
            _loadingTongueScale = tongueScale;
        }
        else if (ReferenceEquals(targetCanvas, SettingsSpriteCanvas) && tongue.RenderTransform is ScaleTransform settingsTongueScale)
        {
            _settingsTongueScale = settingsTongueScale;
        }
        Canvas.SetLeft(tongue, 262);
        Canvas.SetTop(tongue, 82);
        AddStripedTongue(tongue, 0, 7, 112, 7, pink1, pink2, 7);
        AddRect(tongue, 112, 7, 14, 7, pink1);
        var tongueTip = AddRect(tongue, 119, 0, 7, 21, pink1);
        if (ReferenceEquals(targetCanvas, LoadingSpriteCanvas))
        {
            _loadingTongueTip = tongueTip;
        }
        else if (ReferenceEquals(targetCanvas, SettingsSpriteCanvas))
        {
            _settingsTongueTip = tongueTip;
        }
        targetCanvas.Children.Add(tongue);

        var chameleon = new Canvas
        {
            Width = 231,
            Height = 84,
            RenderTransform = new TranslateTransform()
        };
        if (ReferenceEquals(targetCanvas, LoadingSpriteCanvas) && chameleon.RenderTransform is TranslateTransform spriteTransform)
        {
            _loadingSpriteTransform = spriteTransform;
        }
        else if (ReferenceEquals(targetCanvas, SettingsSpriteCanvas) && chameleon.RenderTransform is TranslateTransform settingsSpriteTransform)
        {
            _settingsSpriteTransform = settingsSpriteTransform;
        }
        Canvas.SetLeft(chameleon, 65);
        Canvas.SetTop(chameleon, 51);
        targetCanvas.Children.Add(chameleon);

        AddPixels(chameleon, 33, 39, pixel, green3,
            (7, 0, green3), (14, 0, green2), (21, 0, green2),
            (21, 7, green2), (21, 14, green2), (14, 14, green2),
            (7, 14, green3), (0, 14, green3), (-7, 14, green3), (-14, 14, green3),
            (-14, 7, green3), (-7, 7, screen), (0, 7, screen), (7, 7, screen));

        AddPixels(chameleon, 70, 25, pixel, green2,
            (7, 0, green2), (14, 0, green1), (21, 0, green1), (28, 0, green1), (35, 0, green1),
            (42, 0, green2), (49, 0, green2), (56, 0, green2), (63, 0, green3), (70, 0, green3),
            (-7, 7, green2), (0, 7, green1), (7, 7, green1), (14, 7, green1), (21, 7, lime),
            (28, 7, green1), (35, 7, green2), (42, 7, green1), (49, 7, green2), (56, 7, green2),
            (63, 7, green3), (70, 7, green3), (77, 7, green3),
            (-7, 14, green3), (0, 14, green2), (7, 14, green1), (14, 14, lime), (21, 14, lime),
            (28, 14, lime), (35, 14, green1), (42, 14, green2), (49, 14, green2), (56, 14, green2),
            (63, 14, green3), (70, 14, green3), (77, 14, green3),
            (0, 21, green3), (7, 21, green2), (14, 21, green1), (21, 21, green1), (28, 21, lime),
            (35, 21, green1), (42, 21, green2), (49, 21, green2), (56, 21, green3), (63, 21, green3),
            (70, 21, green3), (14, 28, green3), (21, 28, green2), (28, 28, green2), (35, 28, green2),
            (42, 28, green3), (49, 28, green3));

        AddPixels(chameleon, 147, 18, pixel, green1,
            (7, 0, green1), (14, 0, green2), (21, 0, green2), (28, 0, green3),
            (0, 7, green1), (7, 7, green1), (14, 7, green2), (21, 7, green2), (28, 7, green2), (35, 7, green3),
            (0, 14, green2), (7, 14, green2), (14, 14, green2), (21, 14, green3), (28, 14, green3), (35, 14, green3), (42, 14, green3),
            (7, 21, green3), (14, 21, green3), (21, 21, green3), (28, 21, green3));

        var eye = new Canvas
        {
            RenderTransform = new TranslateTransform()
        };
        if (ReferenceEquals(targetCanvas, LoadingSpriteCanvas) && eye.RenderTransform is TranslateTransform eyeTransform)
        {
            _loadingEyeTransform = eyeTransform;
        }
        else if (ReferenceEquals(targetCanvas, SettingsSpriteCanvas) && eye.RenderTransform is TranslateTransform settingsEyeTransform)
        {
            _settingsEyeTransform = settingsEyeTransform;
        }
        Canvas.SetLeft(eye, 161);
        Canvas.SetTop(eye, 4);
        chameleon.Children.Add(eye);
        AddPixels(eye, 0, 0, pixel, white, (7, 0, white), (0, 7, white), (7, 7, black));

        AddPixels(chameleon, 189, 39, pixel, black, (7, 0, black), (14, 0, black));
        AddPixels(chameleon, 84, 63, pixel, green3, (0, 7, green3), (0, 14, green3), (7, 14, green3), (14, 14, green3));
        AddPixels(chameleon, 138, 63, pixel, green3, (0, 7, green3), (0, 14, green3), (7, 14, green3), (14, 14, green3));
    }

    private static Rectangle AddRect(Canvas canvas, double left, double top, double width, double height, string color, double opacity = 1)
    {
        var rect = new Rectangle
        {
            Width = width,
            Height = height,
            Fill = BrushFromHex(color),
            Opacity = opacity,
            SnapsToDevicePixels = true
        };
        Canvas.SetLeft(rect, left);
        Canvas.SetTop(rect, top);
        canvas.Children.Add(rect);
        return rect;
    }

    private static void AddPixels(Canvas canvas, int left, int top, int size, string color, params (int X, int Y, string Color)[] pixels)
    {
        AddRect(canvas, left, top, size, size, color);
        foreach (var pixel in pixels)
        {
            AddRect(canvas, left + pixel.X, top + pixel.Y, size, size, pixel.Color);
        }
    }

    private static void AddStripedTongue(Canvas canvas, int left, int top, int width, int height, string light, string dark, int stripeWidth)
    {
        for (var x = 0; x < width; x += stripeWidth)
        {
            AddRect(canvas, left + x, top, Math.Min(stripeWidth, width - x), height, (x / stripeWidth) % 2 == 0 ? dark : light);
        }
    }

    private static Brush BrushFromHex(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    private void LoadAppSettings()
    {
        _isApplyingSettings = true;
        try
        {
            var settings = _settings.Load();

            TopmostToggle.IsChecked = settings.Topmost;
            Topmost = settings.Topmost;
            RightDockToggle.IsChecked = settings.RightDock;

            FontSizeSlider.Value = settings.TodoFontSize;
            SubFontSizeSlider.Value = settings.SubTodoFontSize;
            MemoFontSizeSlider.Value = settings.MemoFontSize;
            StickyNoteFontSizeSlider.Value = settings.StickyNoteFontSize;
            StickyNoteTopmostToggle.IsChecked = settings.StickyNoteTopmost;
            _stickyNoteTopmost = settings.StickyNoteTopmost;
            AutoStickyNewMemoToggle.IsChecked = settings.AutoStickyNewMemos;
            _autoStickyNewMemos = settings.AutoStickyNewMemos;
            AddButtonSize = 16;
            MainSpacingSlider.Value = settings.MainSpacing;
            ListSpacingSlider.Value = settings.SubSpacing;
            _stickyArrangeDirection = settings.StickyArrangeDirection;
            SelectStickyArrangeDirection(_stickyArrangeDirection);

            var selectedTheme = false;
            foreach (var item in ThemeCombo.Items.OfType<ComboBoxItem>())
            {
                if (string.Equals(item.Content?.ToString(), settings.Theme, StringComparison.Ordinal))
                {
                    item.IsSelected = true;
                    selectedTheme = true;
                    break;
                }
            }

            if (!selectedTheme && ThemeCombo.Items.OfType<ComboBoxItem>().FirstOrDefault() is { } firstTheme)
            {
                firstTheme.IsSelected = true;
            }

            _currentTheme = (ThemeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? settings.Theme;
            ApplyTheme(_currentTheme);
        }
        finally
        {
            _isApplyingSettings = false;
        }
    }

    private void SaveAppSettings()
    {
        if (_isApplyingSettings ||
            TopmostToggle is null ||
            RightDockToggle is null ||
            ThemeCombo is null ||
            FontSizeSlider is null ||
            SubFontSizeSlider is null ||
            MemoFontSizeSlider is null ||
            StickyNoteFontSizeSlider is null ||
            StickyNoteTopmostToggle is null ||
            AutoStickyNewMemoToggle is null ||
            MainSpacingSlider is null ||
            ListSpacingSlider is null ||
            StickyArrangeDirectionCombo is null)
        {
            return;
        }

        _settings.Save(new AppSettings
        {
            Topmost = TopmostToggle.IsChecked == true,
            RightDock = RightDockToggle.IsChecked == true,
            Theme = _currentTheme,
            TodoFontSize = TodoFontSize,
            SubTodoFontSize = SubTodoFontSize,
            MemoFontSize = MemoFontSize,
            StickyNoteFontSize = StickyNoteFontSize,
            StickyNoteTopmost = _stickyNoteTopmost,
            AutoStickyNewMemos = _autoStickyNewMemos,
            AddButtonSize = 16,
            MainSpacing = MainSpacingSlider?.Value ?? MainItemPadding.Top,
            SubSpacing = ListSpacingSlider?.Value ?? SubRowHeight,
            StickyArrangeDirection = _stickyArrangeDirection
        });
    }
    private async Task LoadTodosAsync()
    {
        _isLoading = true;
        try
        {
            foreach (var todo in Todos)
            {
                UnsubscribeTodo(todo);
            }

            Todos.Clear();

            var loaded = await _storage.LoadAsync();
            foreach (var todo in loaded.OrderByDescending(todo => todo.IsPinned).ThenBy(todo => todo.IsCompleted).ThenByDescending(todo => todo.CreatedAt))
            {
                SubscribeTodo(todo);
                Todos.Add(todo);
            }

            RefreshRecurringTodos();
            UpdateCount();
            RefreshFilter();
            StatusText.Text = "불러옴";
        }
        catch (Exception ex)
        {
            StatusText.Text = "불러오기 실패";
            MessageBox.Show(this, ex.Message, "불러오기 실패", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void RestoreStickyNotes()
    {
        foreach (var memo in Todos.Where(todo => todo.IsMemo && !todo.IsDeleted && todo.IsStickyNoteEnabled).ToList())
        {
            ShowStickyNote(memo, focusEditor: false);
        }
    }
    private void AddTodo_Click(object sender, RoutedEventArgs e)
    {
        AddTodo();
    }

    private void AddMemo_Click(object sender, RoutedEventArgs e)
    {
        AddMemo();
    }

    private void NewTodoBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            return;
        }

        if (_activeFilter == "Memo")
        {
            AddMemo();
        }
        else
        {
            AddTodo();
        }

        e.Handled = true;
    }

    private void AddTodo()
    {
        AddEntry(isMemo: false);
    }

    private void AddMemo()
    {
        AddEntry(isMemo: true);
    }

    private void AddEntry(bool isMemo)
    {
        var title = NewTodoBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        if (Todos.FirstOrDefault() is { } firstTodo &&
            firstTodo.IsMemo == isMemo &&
            !firstTodo.IsDeleted &&
            !firstTodo.IsCompleted &&
            string.Equals(firstTodo.Title, title, StringComparison.Ordinal) &&
            DateTimeOffset.Now - firstTodo.CreatedAt < TimeSpan.FromSeconds(2))
        {
            NewTodoBox.Clear();
            NewTodoBox.Focus();
            return;
        }

        var entry = new TodoItem
        {
            Title = title,
            IsMemo = isMemo,
            IsStickyNoteEnabled = isMemo && _autoStickyNewMemos,
            CreatedAt = DateTimeOffset.Now
        };
        Todos.Insert(0, entry);

        _activeFilter = isMemo ? "Memo" : "Open";
        if (isMemo)
        {
            _showMemoTrash = false;
        }

        NewTodoBox.Clear();
        NewTodoBox.Focus();
        UpdateCount();
        RefreshFilter();
        ApplyFilterButtonState();
        StatusText.Text = "저장 중...";

        if (entry.IsMemo && entry.IsStickyNoteEnabled)
        {
            ShowStickyNote(entry, focusEditor: false);
        }
    }

    private void DeleteTodo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: TodoItem todo })
        {
            UnsubscribeTodo(todo);
            Todos.Remove(todo);
            StatusText.Text = "저장 중...";
        }
    }

    private void ShowSubTodoInput_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: TodoItem parent } || parent.IsCompleted)
        {
            return;
        }

        parent.IsAddingSubTask = true;
    }

    private void AddSubTodoInline_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: TodoItem parent })
        {
            return;
        }

        AddSubTodo(parent);
    }

    private void SubTodoBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox { DataContext: TodoItem parent })
        {
            return;
        }

        AddSubTodo(parent);
        e.Handled = true;
    }

    private void SubTodoBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox { DataContext: TodoItem parent })
        {
            return;
        }

        if (e.NewFocus is DependencyObject nextFocus && FindAncestor<Button>(nextFocus) is not null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(parent.SubTaskDraft))
        {
            parent.SubTaskDraft = string.Empty;
            parent.IsAddingSubTask = false;
        }
    }

    private void AddSubTodo(TodoItem parent)
    {
        if (parent.IsCompleted)
        {
            parent.SubTaskDraft = string.Empty;
            parent.IsAddingSubTask = false;
            return;
        }

        var title = parent.SubTaskDraft;
        if (string.IsNullOrWhiteSpace(title))
        {
            parent.SubTaskDraft = string.Empty;
            parent.IsAddingSubTask = false;
            return;
        }

        parent.SubTasks.Add(new TodoItem
        {
            Title = title.Trim(),
            CreatedAt = DateTimeOffset.Now
        });

        parent.SubTaskDraft = string.Empty;
        parent.IsAddingSubTask = false;
        StatusText.Text = "저장 중...";
    }

    private void TodoChanged_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { DataContext: TodoItem todo } checkBox)
        {
            return;
        }

        var isChecked = checkBox.IsChecked == true;
        todo.IsCompleted = isChecked;
        if (isChecked)
        {
            todo.SubTaskDraft = string.Empty;
            todo.IsAddingSubTask = false;
        }

        if (isChecked && IsRecurringTodo(todo))
        {
            _isSchedulingRecurringTodo = true;
            try
            {
                ScheduleNextRecurringTodo(todo);
            }
            finally
            {
                _isSchedulingRecurringTodo = false;
            }
        }
        else if (!isChecked && IsRecurringTodo(todo) && Todos.Contains(todo))
        {
            todo.NextActivationDate = null;
            todo.DueDate ??= GetDueDateForActivation(todo, DateTime.Today);
        }

        ScheduleSave();
        UpdateCount();
        RefreshFilter();
        e.Handled = true;
    }

    private void SubTodoChanged_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { DataContext: TodoItem subTodo } checkBox)
        {
            return;
        }

        subTodo.IsCompleted = checkBox.IsChecked == true;
        ScheduleSave();
        UpdateCount();
        RefreshFilter();
        e.Handled = true;
    }

    private void SubTodoCheckBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not CheckBox { DataContext: TodoItem subTodo })
        {
            return;
        }

        ClearSubTodoDragState();
        subTodo.IsCompleted = !subTodo.IsCompleted;
        ScheduleSave();
        UpdateCount();
        RefreshFilter();
        e.Handled = true;
    }

    private void ScheduleNextRecurringTodo(TodoItem completedTodo)
    {
        if (!Todos.Contains(completedTodo) ||
            string.IsNullOrWhiteSpace(completedTodo.RepeatOption) ||
            completedTodo.RepeatOption == "없음")
        {
            return;
        }

        var nextSchedule = GetNextSchedule(completedTodo);
        if (nextSchedule is null)
        {
            return;
        }

        completedTodo.DueDate = null;
        completedTodo.NextActivationDate = nextSchedule.Value.ActivationDate;
        completedTodo.IsCompleted = true;
        StatusText.Text = "다음 반복일로 이동됨";
    }

    private static (DateTimeOffset ActivationDate, DateTimeOffset DueDate)? GetNextSchedule(TodoItem todo)
    {
        var today = DateTime.Today;

        return todo.RepeatOption switch
        {
            "매일" => CreateSchedule(today.AddDays(1)),
            "매주" => CreateSchedule(GetNextWeeklyDate(today, todo.WeeklyDays)),
            "매월" => CreateMonthlySchedule(GetNextMonthlyDate(today, todo)),
            _ => null
        };
    }

    private static (DateTimeOffset ActivationDate, DateTimeOffset DueDate) CreateSchedule(DateTime dueDate)
    {
        var date = new DateTimeOffset(dueDate);
        return (date, date);
    }

    private static (DateTimeOffset ActivationDate, DateTimeOffset DueDate) CreateMonthlySchedule(DateTime dueDate)
    {
        return (new DateTimeOffset(new DateTime(dueDate.Year, dueDate.Month, 1)), new DateTimeOffset(dueDate));
    }

    private static DateTime GetNextWeeklyDate(DateTime today, IEnumerable<int> weeklyDays)
    {
        var selectedDays = weeklyDays.Any()
            ? weeklyDays.ToList()
            : [(int)today.DayOfWeek];

        return selectedDays
            .Select(day =>
            {
                var diff = (day - (int)today.DayOfWeek + 7) % 7;
                return today.AddDays(diff == 0 ? 7 : diff);
            })
            .OrderBy(date => date)
            .First();
    }

    private static (DateTimeOffset ActivationDate, DateTimeOffset DueDate) GetCurrentOrNextSchedule(TodoItem todo, DateTime today)
    {
        return todo.RepeatOption switch
        {
            "매일" => CreateSchedule(today),
            "매주" => CreateSchedule(GetCurrentOrNextWeeklyDate(today, todo.WeeklyDays)),
            "매월" => CreateMonthlySchedule(GetCurrentOrNextMonthlyDate(today, todo)),
            _ => CreateSchedule(today)
        };
    }

    private static DateTime GetCurrentOrNextWeeklyDate(DateTime today, IEnumerable<int> weeklyDays)
    {
        var selectedDays = weeklyDays.Any()
            ? weeklyDays.ToList()
            : [(int)today.DayOfWeek];

        return selectedDays
            .Select(day =>
            {
                var diff = (day - (int)today.DayOfWeek + 7) % 7;
                return today.AddDays(diff);
            })
            .OrderBy(date => date)
            .First();
    }

    private static DateTime GetNextMonthlyDate(DateTime today, TodoItem todo)
    {
        var nextMonth = new DateTime(today.Year, today.Month, 1).AddMonths(1);
        return todo.MonthlyRepeatMode == "주차요일"
            ? GetNthWeekdayOfMonth(nextMonth.Year, nextMonth.Month, todo.MonthlyDayOfWeek, todo.MonthlyWeek)
            : GetMonthlyDate(nextMonth.Year, nextMonth.Month, todo.MonthlyDay);
    }

    private static DateTime GetCurrentOrNextMonthlyDate(DateTime today, TodoItem todo)
    {
        var currentMonthOccurrence = todo.MonthlyRepeatMode == "주차요일"
            ? GetNthWeekdayOfMonth(today.Year, today.Month, todo.MonthlyDayOfWeek, todo.MonthlyWeek)
            : GetMonthlyDate(today.Year, today.Month, todo.MonthlyDay);

        if (currentMonthOccurrence >= today)
        {
            return currentMonthOccurrence;
        }

        return GetNextMonthlyDate(today, todo);
    }

    private static DateTime GetMonthlyDate(int year, int month, int day)
    {
        var clampedDay = Math.Clamp(day, 1, DateTime.DaysInMonth(year, month));
        return new DateTime(year, month, clampedDay);
    }

    private static DateTime GetNthWeekdayOfMonth(int year, int month, int dayOfWeek, int week)
    {
        if (week >= 5)
        {
            var date = new DateTime(year, month, DateTime.DaysInMonth(year, month));
            while ((int)date.DayOfWeek != dayOfWeek)
            {
                date = date.AddDays(-1);
            }

            return date;
        }

        var first = new DateTime(year, month, 1);
        var offset = (dayOfWeek - (int)first.DayOfWeek + 7) % 7;
        var result = first.AddDays(offset + ((Math.Clamp(week, 1, 4) - 1) * 7));

        if (result.Month != month)
        {
            return GetNthWeekdayOfMonth(year, month, dayOfWeek, 5);
        }

        return result;
    }

    private void RefreshRecurringTodos()
    {
        var today = DateTime.Today;

        foreach (var todo in Todos.Where(IsRecurringTodo).ToList())
        {
            NormalizeRecurringTodo(todo, today);
        }
    }

    private static void NormalizeRecurringTodo(TodoItem todo, DateTime today)
    {
        if (todo.NextActivationDate is { } nextActivationDate)
        {
            if (nextActivationDate.Date > today)
            {
                todo.DueDate = null;
                return;
            }

            ActivateRecurringTodo(todo, nextActivationDate, GetDueDateForActivation(todo, nextActivationDate.Date));
            return;
        }

        MoveRecurringTodoToCurrentOrNextOccurrence(todo, today);
    }

    private static void MoveRecurringTodoToCurrentOrNextOccurrence(TodoItem todo, DateTime today)
    {
        var schedule = GetCurrentOrNextSchedule(todo, today);
        if (schedule.ActivationDate.Date <= today)
        {
            EnsureRecurringTodoActive(todo, schedule.DueDate);
            return;
        }

        todo.IsCompleted = false;
        todo.CompletedAt = null;
        todo.DueDate = null;
        todo.NextActivationDate = schedule.ActivationDate;
    }

    private static void EnsureRecurringTodoActive(TodoItem todo, DateTimeOffset dueDate)
    {
        todo.IsCompleted = false;
        todo.CompletedAt = null;
        todo.DueDate = dueDate;
        todo.NextActivationDate = null;
    }

    private static DateTimeOffset GetDueDateForActivation(TodoItem todo, DateTime activationDate)
    {
        if (todo.RepeatOption == "매월")
        {
            var monthlyDueDate = todo.MonthlyRepeatMode == "주차요일"
                ? GetNthWeekdayOfMonth(activationDate.Year, activationDate.Month, todo.MonthlyDayOfWeek, todo.MonthlyWeek)
                : GetMonthlyDate(activationDate.Year, activationDate.Month, todo.MonthlyDay);

            return new DateTimeOffset(monthlyDueDate);
        }

        return new DateTimeOffset(activationDate);
    }

    private static void ActivateRecurringTodo(TodoItem todo, DateTimeOffset activationDate, DateTimeOffset dueDate)
    {
        todo.IsCompleted = false;
        todo.CompletedAt = null;
        todo.DueDate = dueDate;
        todo.NextActivationDate = null;

        foreach (var subTodo in todo.SubTasks)
        {
            ResetTodoCompletion(subTodo);
        }
    }

    private static void ResetTodoCompletion(TodoItem todo)
    {
        todo.IsCompleted = false;
        todo.CompletedAt = null;

        foreach (var subTodo in todo.SubTasks)
        {
            ResetTodoCompletion(subTodo);
        }
    }

    private static bool IsRecurringTodo(TodoItem todo)
    {
        return !string.IsNullOrWhiteSpace(todo.RepeatOption) && todo.RepeatOption != "없음";
    }

    private static bool IsRecurringWaiting(TodoItem todo)
    {
        return IsRecurringTodo(todo) && todo.NextActivationDate?.Date > DateTimeOffset.Now.Date;
    }

    private void TodoItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (IsInteractiveElement((DependencyObject)e.OriginalSource))
        {
            return;
        }

        if (sender is not FrameworkElement { Tag: TodoItem todo })
        {
            return;
        }

        if (todo.IsMemo)
        {
            return;
        }

        OpenTodoSettings(todo, isSubTask: false);
    }

    private void SubTodoItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (IsInteractiveElement((DependencyObject)e.OriginalSource))
        {
            return;
        }

        if (sender is not FrameworkElement { Tag: TodoItem subTodo })
        {
            return;
        }

        OpenTodoSettings(subTodo, isSubTask: true);
        e.Handled = true;
    }

    private void OpenTodoSettings(TodoItem todo, bool isSubTask)
    {
        var dialog = new Window
        {
            Title = "항목 설정",
            Owner = this,
            Width = 380,
            Height = 460,
            MinWidth = 340,
            MinHeight = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SizeToContent = SizeToContent.Height,
            MaxHeight = SystemParameters.WorkArea.Height - 60,
            ResizeMode = ResizeMode.NoResize,
            Topmost = Topmost,
            Background = (Brush)FindResource("AppBackgroundBrush"),
            Foreground = (Brush)FindResource("TextBrush")
        };
        CopyModalResources(dialog);
        dialog.SourceInitialized += (_, _) => ApplyTitleBarTheme(dialog);

        var root = new Grid
        {
            Margin = new Thickness(18)
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var contentPanel = new StackPanel();
        var scrollContent = new ScrollViewer
        {
            Content = contentPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(0, 0, 6, 0)
        };

        var titleBox = new TextBox
        {
            Text = todo.Title,
            FontSize = 17,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("TodoTextBrush"),
            Background = Brushes.Transparent,
            BorderBrush = (Brush)FindResource("LineBrush"),
            CaretBrush = (Brush)FindResource("TextBrush"),
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 96,
            Margin = new Thickness(0, 0, 0, 16),
            ToolTip = "항목 이름 수정"
        };

        var dueLabel = CreateSettingsLabel("기한");
        var today = DateTimeOffset.Now.Date;
        var dueOption = todo.DueDate is null
            ? "없음"
            : todo.DueDate.Value.Date == today
                ? "오늘"
                : todo.DueDate.Value.Date == today.AddDays(1)
                    ? "내일"
                    : "날짜 지정";
        var dueDateBox = new TextBox
        {
            Text = todo.DueDate?.ToString("yyyy-MM-dd") ?? DateTime.Today.ToString("yyyy-MM-dd"),
            Margin = new Thickness(0, 8, 0, 12),
            Background = (Brush)FindResource("InputBrush"),
            Foreground = (Brush)FindResource("TextBrush"),
            BorderBrush = (Brush)FindResource("LineBrush"),
            CaretBrush = (Brush)FindResource("TextBrush"),
            Visibility = dueOption == "날짜 지정" ? Visibility.Visible : Visibility.Collapsed,
            ToolTip = "yyyy-MM-dd 형식으로 입력"
        };
        var dueGroup = CreateOptionGroup(new[] { "없음", "오늘", "내일", "날짜 지정" }, dueOption);
        foreach (var radio in dueGroup.Children.OfType<ToggleButton>())
        {
            radio.Checked += (_, _) => dueDateBox.Visibility = GetSelectedOption(dueGroup) == "날짜 지정"
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        var repeatLabel = CreateSettingsLabel("반복");
        var repeatGroup = CreateOptionGroup(
            new[] { "없음", "매일", "매주", "매월" },
            string.IsNullOrWhiteSpace(todo.RepeatOption) ? "없음" : todo.RepeatOption);
        repeatGroup.Margin = new Thickness(0, 0, 0, 12);
        StackPanel? buttons = null;

        var weeklyPanel = new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 12),
            Visibility = todo.RepeatOption == "매주" ? Visibility.Visible : Visibility.Collapsed
        };
        weeklyPanel.Children.Add(CreateSettingsLabel("반복 요일"));
        var weeklyDaysPanel = CreateMultiOptionGroup(
            new[] { 1, 2, 3, 4, 5, 6, 0 },
            todo.WeeklyDays.Count > 0 ? todo.WeeklyDays : new ObservableCollection<int> { (int)DateTime.Today.DayOfWeek },
            TodoItem.ToDayLabel);
        weeklyPanel.Children.Add(weeklyDaysPanel);

        var monthlyPanel = new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 12),
            Visibility = todo.RepeatOption == "매월" ? Visibility.Visible : Visibility.Collapsed
        };
        monthlyPanel.Children.Add(CreateSettingsLabel("매월 방식"));
        var monthlyModeGroup = CreateOptionGroup(new[] { "날짜", "주차요일" }, todo.MonthlyRepeatMode);
        monthlyPanel.Children.Add(monthlyModeGroup);

        var monthlyDayPanel = new StackPanel
        {
            Margin = new Thickness(0, 8, 0, 0),
            Visibility = todo.MonthlyRepeatMode == "주차요일" ? Visibility.Collapsed : Visibility.Visible
        };
        monthlyDayPanel.Children.Add(CreateSettingsLabel("반복 날짜"));
        var monthlyDayBox = new TextBox
        {
            Text = todo.MonthlyDay.ToString(),
            Background = (Brush)FindResource("InputBrush"),
            Foreground = (Brush)FindResource("TextBrush"),
            BorderBrush = (Brush)FindResource("LineBrush"),
            CaretBrush = (Brush)FindResource("TextBrush"),
            ToolTip = "1~31 사이 숫자"
        };
        monthlyDayPanel.Children.Add(monthlyDayBox);

        var monthlyWeekdayPanel = new StackPanel
        {
            Margin = new Thickness(0, 8, 0, 0),
            Visibility = todo.MonthlyRepeatMode == "주차요일" ? Visibility.Visible : Visibility.Collapsed
        };
        monthlyWeekdayPanel.Children.Add(CreateSettingsLabel("주차"));
        var monthlyWeekGroup = CreateOptionGroup(new[] { "1", "2", "3", "4", "마지막" }, todo.MonthlyWeek == 5 ? "마지막" : todo.MonthlyWeek.ToString());
        monthlyWeekdayPanel.Children.Add(monthlyWeekGroup);
        monthlyWeekdayPanel.Children.Add(CreateSettingsLabel("요일"));
        var monthlyDayOfWeekGroup = CreateOptionGroup(
            new[] { "월", "화", "수", "목", "금", "토", "일" },
            TodoItem.ToDayLabel(todo.MonthlyDayOfWeek));
        monthlyWeekdayPanel.Children.Add(monthlyDayOfWeekGroup);
        monthlyPanel.Children.Add(monthlyDayPanel);
        monthlyPanel.Children.Add(monthlyWeekdayPanel);

        foreach (var radio in repeatGroup.Children.OfType<ToggleButton>())
        {
            radio.Checked += (_, _) =>
            {
                var selectedRepeat = GetSelectedOption(repeatGroup);
                weeklyPanel.Visibility = selectedRepeat == "매주" ? Visibility.Visible : Visibility.Collapsed;
                monthlyPanel.Visibility = selectedRepeat == "매월" ? Visibility.Visible : Visibility.Collapsed;
                if (buttons is not null)
                {
                    FitTodoSettingsDialog(dialog, scrollContent, contentPanel, buttons);
                }
            };
        }

        foreach (var radio in monthlyModeGroup.Children.OfType<ToggleButton>())
        {
            radio.Checked += (_, _) =>
            {
                var selectedMode = GetSelectedOption(monthlyModeGroup);
                monthlyDayPanel.Visibility = selectedMode == "주차요일" ? Visibility.Collapsed : Visibility.Visible;
                monthlyWeekdayPanel.Visibility = selectedMode == "주차요일" ? Visibility.Visible : Visibility.Collapsed;
                if (buttons is not null)
                {
                    FitTodoSettingsDialog(dialog, scrollContent, contentPanel, buttons);
                }
            };
        }

        var pinnedCheck = new ToggleButton
        {
            IsChecked = todo.IsPinned,
            Foreground = (Brush)FindResource("TextBrush"),
            Margin = new Thickness(0, 0, 0, 12),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        pinnedCheck.Template = CreateFlatToggleTemplate();
        pinnedCheck.Content = CreateOptionContent("상단 고정", todo.IsPinned);
        pinnedCheck.Checked += (_, _) => pinnedCheck.Content = CreateOptionContent("상단 고정", true);
        pinnedCheck.Unchecked += (_, _) => pinnedCheck.Content = CreateOptionContent("상단 고정", false);

        var noteLabel = CreateSettingsLabel("메모");
        var noteBox = new TextBox
        {
            Text = todo.Note,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MinHeight = 110,
            Margin = new Thickness(0, 0, 0, 14),
            Background = (Brush)FindResource("InputBrush"),
            Foreground = (Brush)FindResource("TextBrush"),
            BorderBrush = (Brush)FindResource("LineBrush"),
            CaretBrush = (Brush)FindResource("TextBrush")
        };

        buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        var deleteButton = new Button
        {
            Content = "삭제",
            MinWidth = 64,
            Margin = new Thickness(0, 0, 8, 0),
            Foreground = (Brush)FindResource("DangerBrush"),
            Style = (Style)FindResource("ShellButtonStyle")
        };
        var saveButton = new Button
        {
            Content = "저장",
            MinWidth = 64,
            Margin = new Thickness(0, 0, 8, 0),
            Foreground = (Brush)FindResource("AccentBrush"),
            Style = (Style)FindResource("ShellButtonStyle")
        };
        var cancelButton = new Button
        {
            Content = "취소",
            MinWidth = 64,
            Foreground = (Brush)FindResource("TextMutedBrush"),
            Style = (Style)FindResource("ShellButtonStyle")
        };
        deleteButton.Click += (_, _) =>
        {
            dialog.Tag = "delete";
            dialog.DialogResult = true;
        };
        saveButton.Click += (_, _) => dialog.DialogResult = true;
        cancelButton.Click += (_, _) => dialog.DialogResult = false;
        buttons.Children.Add(deleteButton);
        buttons.Children.Add(saveButton);
        buttons.Children.Add(cancelButton);

        contentPanel.Children.Add(titleBox);

        var duePanel = new StackPanel();
        duePanel.Children.Add(dueLabel);
        duePanel.Children.Add(dueGroup);
        duePanel.Children.Add(dueDateBox);
        contentPanel.Children.Add(duePanel);

        var repeatPanel = new StackPanel();
        repeatPanel.Children.Add(repeatLabel);
        repeatPanel.Children.Add(repeatGroup);
        repeatPanel.Children.Add(weeklyPanel);
        repeatPanel.Children.Add(monthlyPanel);
        repeatPanel.Visibility = isSubTask ? Visibility.Collapsed : Visibility.Visible;
        contentPanel.Children.Add(repeatPanel);

        pinnedCheck.Visibility = isSubTask ? Visibility.Collapsed : Visibility.Visible;
        contentPanel.Children.Add(pinnedCheck);

        var notePanel = new StackPanel();
        notePanel.Children.Add(noteLabel);
        notePanel.Children.Add(noteBox);
        contentPanel.Children.Add(notePanel);

        Grid.SetRow(scrollContent, 0);
        root.Children.Add(scrollContent);

        Grid.SetRow(buttons, 1);
        root.Children.Add(buttons);

        dialog.Content = root;
        dialog.Loaded += (_, _) => FitTodoSettingsDialog(dialog, scrollContent, contentPanel, buttons);


        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (dialog.Tag as string == "delete")
        {
            UnsubscribeTodo(todo);
            if (isSubTask)
            {
                RemoveSubTodo(todo);
            }
            else
            {
                Todos.Remove(todo);
            }
            StatusText.Text = "저장 중...";
            ScheduleSave();
            return;
        }

        var selectedDueOption = GetSelectedOption(dueGroup);
        var updatedTitle = titleBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(updatedTitle))
        {
            todo.Title = updatedTitle;
        }

        todo.DueDate = selectedDueOption switch
        {
            "오늘" => new DateTimeOffset(DateTime.Today),
            "내일" => new DateTimeOffset(DateTime.Today.AddDays(1)),
            "날짜 지정" when DateTime.TryParse(dueDateBox.Text, out var selectedDate) => new DateTimeOffset(selectedDate),
            _ => null
        };
        if (!isSubTask)
        {
            todo.RepeatOption = GetSelectedOption(repeatGroup);
            todo.WeeklyDays = new ObservableCollection<int>(GetSelectedValues(weeklyDaysPanel));
            todo.MonthlyRepeatMode = GetSelectedOption(monthlyModeGroup);
            todo.MonthlyDay = int.TryParse(monthlyDayBox.Text, out var parsedMonthlyDay)
                ? Math.Clamp(parsedMonthlyDay, 1, 31)
                : 1;
            todo.MonthlyWeek = GetSelectedOption(monthlyWeekGroup) == "마지막"
                ? 5
                : int.TryParse(GetSelectedOption(monthlyWeekGroup), out var parsedWeek)
                    ? Math.Clamp(parsedWeek, 1, 5)
                    : 1;
            todo.MonthlyDayOfWeek = FromDayLabel(GetSelectedOption(monthlyDayOfWeekGroup));
            if (todo.RepeatOption == "없음")
            {
                todo.NextActivationDate = null;
            }
            todo.IsPinned = pinnedCheck.IsChecked == true;
        }
        todo.Note = noteBox.Text.Trim();

        if (!isSubTask && todo.IsPinned)
        {
            var index = Todos.IndexOf(todo);
            if (index > 0)
            {
                Todos.Move(index, 0);
            }
        }

        StatusText.Text = "저장 중...";
        UpdateCount();
        RefreshFilter();
        ScheduleSave();
    }

    private static void FitTodoSettingsDialog(
        Window dialog,
        ScrollViewer scrollContent,
        FrameworkElement contentPanel,
        FrameworkElement buttons)
    {
        dialog.Dispatcher.BeginInvoke(() =>
        {
            var maxHeight = Math.Max(dialog.MinHeight, SystemParameters.WorkArea.Height - 60);
            var availableWidth = Math.Max(1, dialog.Width - 54);
            contentPanel.Measure(new Size(availableWidth, double.PositiveInfinity));
            buttons.Measure(new Size(availableWidth, double.PositiveInfinity));

            const double chromeAndMargins = 92;
            var desiredHeight = contentPanel.DesiredSize.Height + buttons.DesiredSize.Height + chromeAndMargins;
            if (desiredHeight <= maxHeight)
            {
                scrollContent.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                scrollContent.MaxHeight = double.PositiveInfinity;
                dialog.MaxHeight = maxHeight;
                dialog.Height = Math.Max(dialog.MinHeight, desiredHeight);
                return;
            }

            scrollContent.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scrollContent.MaxHeight = Math.Max(160, maxHeight - buttons.DesiredSize.Height - chromeAndMargins);
            dialog.Height = maxHeight;
        }, DispatcherPriority.Background);
    }

    private bool RemoveSubTodo(TodoItem subTodo)
    {
        foreach (var parent in Todos)
        {
            if (RemoveSubTodo(parent, subTodo))
            {
                return true;
            }
        }

        return false;
    }

    private bool RemoveSubTodo(TodoItem parent, TodoItem target)
    {
        if (parent.SubTasks.Contains(target))
        {
            parent.SubTasks.Remove(target);
            return true;
        }

        return parent.SubTasks.Any(child => RemoveSubTodo(child, target));
    }

    private TextBlock CreateSettingsLabel(string text)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = (Brush)FindResource("TextMutedBrush"),
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 6)
        };
    }

    private void CopyModalResources(Window dialog)
    {
        foreach (var key in new[]
        {
            "AppBackgroundBrush",
            "PanelBrush",
            "PanelSoftBrush",
            "InputBrush",
            "LineBrush",
            "TextBrush",
            "TodoTextBrush",
            "SubTodoTextBrush",
            "CompletedTextBrush",
            "TextMutedBrush",
            "AccentBrush",
            "DangerBrush",
            "CheckBrush"
        })
        {
            dialog.Resources[key] = FindResource(key);
        }

        dialog.Resources[typeof(ScrollBar)] = FindResource(typeof(ScrollBar));
        dialog.Resources[typeof(ScrollViewer)] = FindResource(typeof(ScrollViewer));
        dialog.Resources[SystemParameters.VerticalScrollBarWidthKey] = 3.0;
        dialog.Resources[SystemParameters.HorizontalScrollBarHeightKey] = 3.0;
    }

    private WrapPanel CreateOptionGroup(IEnumerable<string> options, string selectedOption)
    {
        var groupName = $"option_{Guid.NewGuid():N}";
        var panel = new WrapPanel
        {
            Margin = new Thickness(0, 0, 0, 8)
        };

        foreach (var option in options)
        {
            var button = new ToggleButton
            {
                IsChecked = option == selectedOption,
                Tag = option,
                Foreground = (Brush)FindResource("TextBrush"),
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            button.Template = CreateFlatToggleTemplate();
            button.Content = CreateOptionContent(option, option == selectedOption);
            button.Checked += (_, _) =>
            {
                foreach (var sibling in panel.Children.OfType<ToggleButton>().Where(sibling => !ReferenceEquals(sibling, button)))
                {
                    sibling.IsChecked = false;
                    sibling.Content = CreateOptionContent(sibling.Tag?.ToString() ?? string.Empty, false);
                }

                button.IsChecked = true;
                button.Content = CreateOptionContent(option, true);
            };
            button.Unchecked += (_, _) =>
            {
                if (!panel.Children.OfType<ToggleButton>().Any(sibling => sibling.IsChecked == true))
                {
                    button.IsChecked = true;
                    button.Content = CreateOptionContent(option, true);
                    return;
                }

                button.Content = CreateOptionContent(option, false);
            };

            panel.Children.Add(button);
        }

        return panel;
    }

    private WrapPanel CreateMultiOptionGroup(IEnumerable<int> options, IEnumerable<int> selectedOptions, Func<int, string> labelSelector)
    {
        var selected = selectedOptions.ToHashSet();
        var panel = new WrapPanel
        {
            Margin = new Thickness(0, 0, 0, 8)
        };

        foreach (var option in options)
        {
            var isSelected = selected.Contains(option);
            var button = new ToggleButton
            {
                IsChecked = isSelected,
                Tag = option,
                Foreground = (Brush)FindResource("TextBrush"),
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Template = CreateFlatToggleTemplate()
            };
            button.Content = CreateOptionContent(labelSelector(option), isSelected);
            button.Checked += (_, _) => button.Content = CreateOptionContent(labelSelector(option), true);
            button.Unchecked += (_, _) =>
            {
                if (!panel.Children.OfType<ToggleButton>().Any(child => !ReferenceEquals(child, button) && child.IsChecked == true))
                {
                    button.IsChecked = true;
                    button.Content = CreateOptionContent(labelSelector(option), true);
                    return;
                }

                button.Content = CreateOptionContent(labelSelector(option), false);
            };

            panel.Children.Add(button);
        }

        return panel;
    }

    private static string GetSelectedOption(Panel panel)
    {
        return panel.Children
            .OfType<ToggleButton>()
            .FirstOrDefault(button => button.IsChecked == true)?
            .Tag?.ToString() ?? "없음";
    }

    private static IEnumerable<int> GetSelectedValues(Panel panel)
    {
        return panel.Children
            .OfType<ToggleButton>()
            .Where(button => button.IsChecked == true)
            .Select(button => button.Tag)
            .OfType<int>();
    }

    private static int FromDayLabel(string label)
    {
        return label switch
        {
            "일" => 0,
            "월" => 1,
            "화" => 2,
            "수" => 3,
            "목" => 4,
            "금" => 5,
            "토" => 6,
            _ => 1
        };
    }

    private StackPanel CreateOptionContent(string text, bool isSelected)
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children =
            {
                new Ellipse
                {
                    Width = 7,
                    Height = 7,
                    Fill = isSelected ? (Brush)FindResource("CheckBrush") : new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                    Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center
                },
                new TextBlock
                {
                    Text = text,
                    Foreground = isSelected ? Brushes.White : (Brush)FindResource("TextMutedBrush"),
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center
                }
            }
        };
    }

    private static ControlTemplate CreateFlatToggleTemplate()
    {
        var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
        contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

        var template = new ControlTemplate(typeof(ToggleButton))
        {
            VisualTree = contentPresenter
        };

        return template;
    }

    private void TopmostToggle_Changed(object sender, RoutedEventArgs e)
    {
        Topmost = TopmostToggle.IsChecked == true;
        SaveAppSettings();
    }

    private void RightDockToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (RightDockToggle.IsChecked == true)
        {
            _rightDockRestoreBounds ??= new Rect(Left, Top, Width, Height);
            DockToRight();
        }
        else
        {
            RestoreFromRightDock();
        }

        SaveAppSettings();
    }

    private void DockToRight()
    {
        const double topOffset = 100;
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width;
        Top = workArea.Top + topOffset;
        Height = Math.Max(MinHeight, workArea.Height - topOffset);
    }

    private void RestoreFromRightDock()
    {
        if (_rightDockRestoreBounds is not { } restoreBounds)
        {
            return;
        }

        Left = restoreBounds.Left;
        Top = restoreBounds.Top;
        Width = restoreBounds.Width;
        Height = restoreBounds.Height;
        _rightDockRestoreBounds = null;
    }

    private void SelectStickyArrangeDirection(string direction)
    {
        foreach (var item in StickyArrangeDirectionCombo.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), direction, StringComparison.Ordinal))
            {
                item.IsSelected = true;
                return;
            }
        }
    }

    private void StickyArrangeDirectionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StickyArrangeDirectionCombo.SelectedItem is ComboBoxItem item)
        {
            _stickyArrangeDirection = item.Tag?.ToString() ?? "Horizontal";
            SaveAppSettings();
        }
    }
    private void SettingsReset_Click(object sender, RoutedEventArgs e)
    {
        var defaults = new AppSettings();
        _isApplyingSettings = true;
        try
        {
            TopmostToggle.IsChecked = defaults.Topmost;
            Topmost = defaults.Topmost;
            RightDockToggle.IsChecked = defaults.RightDock;

            if (ThemeCombo.Items.OfType<ComboBoxItem>().FirstOrDefault() is { } firstTheme)
            {
                firstTheme.IsSelected = true;
                _currentTheme = firstTheme.Content?.ToString() ?? defaults.Theme;
            }
            else
            {
                _currentTheme = defaults.Theme;
            }

            FontSizeSlider.Value = defaults.TodoFontSize;
            SubFontSizeSlider.Value = defaults.SubTodoFontSize;
            MemoFontSizeSlider.Value = defaults.MemoFontSize;
            StickyNoteFontSizeSlider.Value = defaults.StickyNoteFontSize;
            StickyNoteTopmostToggle.IsChecked = defaults.StickyNoteTopmost;
            _stickyNoteTopmost = defaults.StickyNoteTopmost;
            AutoStickyNewMemoToggle.IsChecked = defaults.AutoStickyNewMemos;
            _autoStickyNewMemos = defaults.AutoStickyNewMemos;
            AddButtonSize = 16;
            MainSpacingSlider.Value = defaults.MainSpacing;
            ListSpacingSlider.Value = defaults.SubSpacing;
            _stickyArrangeDirection = defaults.StickyArrangeDirection;
            SelectStickyArrangeDirection(_stickyArrangeDirection);
            ApplyTheme(_currentTheme);
        }
        finally
        {
            _isApplyingSettings = false;
        }

        if (RightDockToggle.IsChecked == true)
        {
            DockToRight();
        }
        else
        {
            RestoreFromRightDock();
        }

        SaveAppSettings();
    }
    private void SettingsToggle_Changed(object sender, RoutedEventArgs e)
    {
        var isOpen = SettingsToggle.IsChecked == true;
        SettingsPanel.Visibility = isOpen ? Visibility.Visible : Visibility.Collapsed;
        SettingsToggle.Content = isOpen ? "▴" : "▾";
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeCombo.SelectedItem is not ComboBoxItem item || item.Content is null)
        {
            return;
        }

        _currentTheme = item.Content.ToString() ?? "기본";
        ApplyTheme(_currentTheme);
        SaveAppSettings();
    }

    private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        TodoFontSize = e.NewValue;
        if (FontSizeValueText is not null)
        {
            FontSizeValueText.Text = e.NewValue.ToString("0");
        }

        SaveAppSettings();
    }

    private void SubFontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        SubTodoFontSize = e.NewValue;
        if (SubFontSizeValueText is not null)
        {
            SubFontSizeValueText.Text = e.NewValue.ToString("0");
        }

        SaveAppSettings();
    }

    private void MemoFontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        MemoFontSize = e.NewValue;
        if (MemoFontSizeValueText is not null)
        {
            MemoFontSizeValueText.Text = e.NewValue.ToString("0");
        }

        SaveAppSettings();
    }

    private void StickyNoteFontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        StickyNoteFontSize = e.NewValue;
        if (StickyNoteFontSizeValueText is not null)
        {
            StickyNoteFontSizeValueText.Text = e.NewValue.ToString("0");
        }

        foreach (var stickyWindow in _stickyNotes.Values)
        {
            if (FindVisualChildByName<TextBox>(stickyWindow, "StickyNoteBox") is { } noteBox)
            {
                noteBox.FontSize = StickyNoteFontSize;
            }
        }

        SaveAppSettings();
    }

    private void StickyNoteTopmostToggle_Changed(object sender, RoutedEventArgs e)
    {
        _stickyNoteTopmost = StickyNoteTopmostToggle.IsChecked == true;
        foreach (var stickyWindow in _stickyNotes.Values)
        {
            stickyWindow.Topmost = _stickyNoteTopmost;
        }

        SaveAppSettings();
    }

    private void AutoStickyNewMemoToggle_Changed(object sender, RoutedEventArgs e)
    {
        _autoStickyNewMemos = AutoStickyNewMemoToggle.IsChecked == true;
        SaveAppSettings();
    }

    private void MainSpacingSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        MainItemPadding = new Thickness(0, e.NewValue, 0, e.NewValue);
        if (MainSpacingValueText is not null)
        {
            MainSpacingValueText.Text = e.NewValue.ToString("0");
        }

        SaveAppSettings();
    }

    private void ListSpacingSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        SubRowHeight = e.NewValue;
        SubItemMargin = new Thickness(0, 0, 0, Math.Max(0, e.NewValue - 24));
        if (SubSpacingValueText is not null)
        {
            SubSpacingValueText.Text = e.NewValue.ToString("0");
        }

        SaveAppSettings();
    }

    private void ApplyTheme(string theme)
    {
        if (theme == "Dark 2026")
        {
            SetBrush("AppBackgroundBrush", "#101114");
            SetBrush("PanelBrush", "#181818");
            SetBrush("PanelSoftBrush", "#2B2B2B");
            SetBrush("InputBrush", "#313131");
            SetBrush("LineBrush", "#2B2B2B");
            SetBrush("TextBrush", "#CCCCCC");
            SetBrush("TodoTextBrush", "#CE9178");
            SetBrush("SubTodoTextBrush", "#B5CEA8");
            SetBrush("CompletedTextBrush", "#6E7681");
            SetBrush("TextMutedBrush", "#9D9D9D");
            SetBrush("AccentBrush", "#DCDCAA");
            SetBrush("DangerBrush", "#DCDCAA");
            SetBrush("CheckBrush", "#2EA043");
            ApplyTitleBarTheme(this);
            ApplyStickyWindowsTheme();
            return;
        }

        SetBrush("AppBackgroundBrush", "#101114");
        SetBrush("PanelBrush", "#181A1F");
        SetBrush("PanelSoftBrush", "#20232B");
        SetBrush("InputBrush", "#20232B");
        SetBrush("LineBrush", "#262A33");
        SetBrush("TextBrush", "#F3F6FA");
        SetBrush("TodoTextBrush", "#F3F6FA");
        SetBrush("SubTodoTextBrush", "#CBD5E1");
        SetBrush("CompletedTextBrush", "#7E8795");
        SetBrush("TextMutedBrush", "#9CA3AF");
        SetBrush("AccentBrush", "#4F9DFF");
        SetBrush("DangerBrush", "#EF6B73");
        SetBrush("CheckBrush", "#22C55E");
        ApplyTitleBarTheme(this);
        ApplyStickyWindowsTheme();
    }

    private void SetBrush(string key, string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        Resources[key] = new SolidColorBrush(color);
    }

    private void ApplyStickyWindowsTheme()
    {
        foreach (var stickyWindow in _stickyNotes.Values)
        {
            CopyModalResources(stickyWindow);
            stickyWindow.Topmost = _stickyNoteTopmost;

            if (stickyWindow.Content is Border border)
            {
                border.SetResourceReference(Border.BackgroundProperty, "PanelBrush");
                border.SetResourceReference(Border.BorderBrushProperty, "LineBrush");
            }

            if (FindVisualChildByName<TextBox>(stickyWindow, "StickyNoteBox") is { } noteBox)
            {
                noteBox.SetResourceReference(Control.ForegroundProperty, "TodoTextBrush");
                noteBox.SetResourceReference(TextBoxBase.CaretBrushProperty, "TextBrush");
            }

            foreach (var button in FindVisualChildren<Button>(stickyWindow))
            {
                button.SetResourceReference(Control.ForegroundProperty, "TextMutedBrush");
            }

            foreach (var text in FindVisualChildren<TextBlock>(stickyWindow))
            {
                text.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");
            }
        }
    }

    private async void Backup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await SaveNowAsync();
            var path = await _storage.CreateBackupAsync();
            StatusText.Text = "백업 생성됨";
            MessageBox.Show(this, path, "백업 생성됨", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusText.Text = "백업 실패";
            MessageBox.Show(this, ex.Message, "백업 실패", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "백업 복원",
            Filter = "JSON backup (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = _storage.BackupDirectory,
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            await _storage.RestoreAsync(dialog.FileName);
            await LoadTodosAsync();
            StatusText.Text = "백업 복원됨";
        }
        catch (Exception ex)
        {
            StatusText.Text = "복원 실패";
            MessageBox.Show(this, ex.Message, "복원 실패", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Todos_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (TodoItem todo in e.OldItems)
            {
                UnsubscribeTodo(todo);
            }
        }

        if (e.NewItems is not null)
        {
            foreach (TodoItem todo in e.NewItems)
            {
                SubscribeTodo(todo);
            }
        }

        UpdateCount();
        ScheduleSave();
        Dispatcher.BeginInvoke(RefreshFilter, DispatcherPriority.Background);
    }

    private void Todo_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TodoItem.SubTaskDraft) or nameof(TodoItem.IsAddingSubTask))
        {
            return;
        }

        if (sender is TodoItem todo &&
            IsRecurringTodo(todo) &&
            (_isSchedulingRecurringTodo || e.PropertyName == nameof(TodoItem.IsCompleted)))
        {
            ScheduleSave();
            return;
        }

        UpdateCount();
        ScheduleSave();
        RefreshFilter();
    }

    private void SubTasks_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (TodoItem todo in e.OldItems)
            {
                UnsubscribeTodo(todo);
            }
        }

        if (e.NewItems is not null)
        {
            foreach (TodoItem todo in e.NewItems)
            {
                SubscribeTodo(todo);
            }
        }

        UpdateCount();
        ScheduleSave();
        RefreshFilter();
    }

    private void SubscribeTodo(TodoItem todo)
    {
        todo.PropertyChanged -= Todo_PropertyChanged;
        todo.PropertyChanged += Todo_PropertyChanged;
        todo.SubTasks.CollectionChanged -= SubTasks_CollectionChanged;
        todo.SubTasks.CollectionChanged += SubTasks_CollectionChanged;

        foreach (var subTodo in todo.SubTasks)
        {
            SubscribeTodo(subTodo);
        }
    }

    private void UnsubscribeTodo(TodoItem todo)
    {
        todo.PropertyChanged -= Todo_PropertyChanged;
        todo.SubTasks.CollectionChanged -= SubTasks_CollectionChanged;

        foreach (var subTodo in todo.SubTasks)
        {
            UnsubscribeTodo(subTodo);
        }
    }

    private void ScheduleSave()
    {
        if (_isLoading)
        {
            return;
        }

        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private async void SaveTimer_Tick(object? sender, EventArgs e)
    {
        _saveTimer.Stop();
        await SaveNowAsync();
    }

    private async Task SaveNowAsync()
    {
        await _storage.SaveAsync(Todos);
        StatusText.Text = $"{DateTime.Now:HH:mm:ss} 저장됨";
    }

    private void UpdateCount()
    {
        RefreshRecurringTodos();

        var open = Todos.Count(todo => !todo.IsDeleted && !todo.IsMemo && !todo.IsCompleted && !IsRecurringWaiting(todo));
        var done = Todos.Count(todo => !todo.IsDeleted && !todo.IsMemo && todo.IsCompleted && !IsRecurringTodo(todo));
        var repeat = Todos.Count(todo => !todo.IsDeleted && !todo.IsMemo && IsRecurringTodo(todo));
        var memo = Todos.Count(todo => todo.IsMemo && !todo.IsDeleted);
        var memoTrash = Todos.Count(todo => todo.IsMemo && todo.IsDeleted);

        OpenFilterButton.Content = $"남은 일 {open}개";
        CompletedFilterButton.Content = $"완료 {done}개";
        RepeatFilterButton.Content = $"반복 {repeat}개";
        MemoFilterButton.Content = $"메모 {memo}개";
        MemoTrashToggleButton.Content = _showMemoTrash ? $"메모 {memo}개" : $"휴지통 {memoTrash}개";
        EmptyMemoTrashButton.Visibility = _activeFilter == "Memo" && _showMemoTrash && memoTrash > 0 ? Visibility.Visible : Visibility.Collapsed;

        ApplyFilterButtonState();
    }

    private void OpenFilter_Click(object sender, RoutedEventArgs e)
    {
        _activeFilter = "Open";
        RefreshFilter();
        ApplyFilterButtonState();
    }

    private void CompletedFilter_Click(object sender, RoutedEventArgs e)
    {
        _activeFilter = "Completed";
        RefreshFilter();
        ApplyFilterButtonState();
    }

    private void RepeatFilter_Click(object sender, RoutedEventArgs e)
    {
        _activeFilter = "Repeat";
        RefreshFilter();
        ApplyFilterButtonState();
    }

    private void MemoFilter_Click(object sender, RoutedEventArgs e)
    {
        _activeFilter = "Memo";
        _showMemoTrash = false;
        RefreshFilter();
        ApplyFilterButtonState();
        UpdateCount();
    }

    private bool FilterTodo(object item)
    {
        if (item is not TodoItem todo)
        {
            return false;
        }

        return _activeFilter switch
        {
            "Completed" => !todo.IsDeleted && !todo.IsMemo && todo.IsCompleted && !IsRecurringTodo(todo),
            "Repeat" => !todo.IsDeleted && !todo.IsMemo && IsRecurringTodo(todo),
            "Memo" => todo.IsMemo && todo.IsDeleted == _showMemoTrash && MemoMatchesSearch(todo),
            _ => !todo.IsDeleted && !todo.IsMemo && !todo.IsCompleted && !IsRecurringWaiting(todo)
        };
    }

    private bool MemoMatchesSearch(TodoItem todo)
    {
        return string.IsNullOrWhiteSpace(_memoSearchText) ||
               todo.Title.Contains(_memoSearchText, StringComparison.OrdinalIgnoreCase);
    }

    private void MemoSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _memoSearchText = MemoSearchBox.Text.Trim();
        if (MemoSearchClearButton is not null)
        {
            MemoSearchClearButton.Visibility = string.IsNullOrWhiteSpace(_memoSearchText) ? Visibility.Collapsed : Visibility.Visible;
        }

        RefreshFilter();
    }

    private void MemoSearchClear_Click(object sender, RoutedEventArgs e)
    {
        MemoSearchBox.Clear();
        MemoSearchBox.Focus();
    }
    private void RefreshFilter()
    {
        CollectionViewSource.GetDefaultView(Todos).Refresh();
    }

    private void ApplyFilterButtonState()
    {
        OpenFilterButton.Foreground = (Brush)FindResource(_activeFilter == "Open" ? "TextBrush" : "TextMutedBrush");
        CompletedFilterButton.Foreground = (Brush)FindResource(_activeFilter == "Completed" ? "TextBrush" : "TextMutedBrush");
        RepeatFilterButton.Foreground = (Brush)FindResource(_activeFilter == "Repeat" ? "TextBrush" : "TextMutedBrush");
        MemoFilterButton.Foreground = (Brush)FindResource(_activeFilter == "Memo" ? "TextBrush" : "TextMutedBrush");
        MemoTrashPanel.Visibility = _activeFilter == "Memo" ? Visibility.Visible : Visibility.Collapsed;
        MemoTrashToggleButton.Foreground = (Brush)FindResource("TextMutedBrush");
        EmptyMemoTrashButton.Visibility = _activeFilter == "Memo" && _showMemoTrash && Todos.Any(todo => todo.IsMemo && todo.IsDeleted) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void MemoTitle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: TodoItem todo } || !todo.IsMemo || todo.IsDeleted)
        {
            return;
        }

        todo.IsEditingTitle = true;
        e.Handled = true;

        Dispatcher.BeginInvoke(() =>
        {
            if (FindVisualChildByName<TextBox>((DependencyObject)sender, "MemoEditBox") is { } box)
            {
                box.Focus();
                box.SelectAll();
            }
        }, DispatcherPriority.Background);
    }

    private void MemoEditBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox { DataContext: TodoItem todo })
        {
            return;
        }

        todo.Title = todo.Title.Trim();
        todo.IsEditingTitle = false;
        e.Handled = true;
    }

    private void MemoEditBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox { DataContext: TodoItem todo })
        {
            todo.Title = todo.Title.Trim();
            todo.IsEditingTitle = false;
        }
    }

    private void StickyMemo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: TodoItem memo } || !memo.IsMemo || memo.IsDeleted)
        {
            return;
        }

        if (_stickyNotes.TryGetValue(memo.Id, out var existing))
        {
            memo.IsStickyNoteEnabled = false;
            existing.Close();
            ScheduleSave();
            return;
        }

        memo.IsStickyNoteEnabled = true;
        ShowStickyNote(memo, focusEditor: false);
        ScheduleSave();
    }

    private void ShowStickyNote(TodoItem memo, bool focusEditor)
    {
        if (_stickyNotes.TryGetValue(memo.Id, out var existing))
        {
            existing.Activate();
            return;
        }

        memo.IsStickyNoteOpen = true;
        memo.IsStickyNoteEnabled = true;

        var noteBox = new TextBox
        {
            Name = "StickyNoteBox",
            Text = memo.Title,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI Symbol, Malgun Gothic"),
            FontSize = StickyNoteFontSize,
            Padding = new Thickness(12, 8, 12, 12),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        noteBox.SetResourceReference(Control.ForegroundProperty, "TodoTextBrush");
        noteBox.SetResourceReference(TextBoxBase.CaretBrushProperty, "TextBrush");
        noteBox.TextChanged += (_, _) =>
        {
            memo.Title = noteBox.Text.Trim();
            ScheduleSave();
            RefreshFilter();
        };

        var arrangeButton = new Button
        {
            Content = "정렬",
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 2, 8, 2),
            Cursor = Cursors.Hand
        };
        arrangeButton.SetResourceReference(Control.ForegroundProperty, "TextMutedBrush");

        var addButton = new Button
        {
            Content = "+",
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 2, 8, 2),
            Cursor = Cursors.Hand
        };
        addButton.SetResourceReference(Control.ForegroundProperty, "TextMutedBrush");

        var closeButton = new Button
        {
            Content = "x",
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 2, 8, 2),
            Cursor = Cursors.Hand
        };
        closeButton.SetResourceReference(Control.ForegroundProperty, "TextMutedBrush");

        var titleBar = new DockPanel
        {
            LastChildFill = true
        };
        titleBar.SetResourceReference(Panel.BackgroundProperty, "PanelSoftBrush");
        DockPanel.SetDock(closeButton, Dock.Right);
        DockPanel.SetDock(addButton, Dock.Right);
        DockPanel.SetDock(arrangeButton, Dock.Right);
        titleBar.Children.Add(closeButton);
        titleBar.Children.Add(addButton);
        titleBar.Children.Add(arrangeButton);
        titleBar.Children.Add(new TextBlock
        {
            Text = "스티키노트",
            FontSize = 12,
            Padding = new Thickness(10, 5, 0, 5),
            VerticalAlignment = VerticalAlignment.Center
        });

        var layout = new DockPanel
        {
        };
        layout.SetResourceReference(Panel.BackgroundProperty, "PanelBrush");
        DockPanel.SetDock(titleBar, Dock.Top);
        layout.Children.Add(titleBar);
        layout.Children.Add(noteBox);

        var stickyWindow = new Window
        {
            Width = memo.StickyWidth ?? 260,
            Height = memo.StickyHeight ?? 220,
            MinWidth = 180,
            MinHeight = 140,
            WindowStartupLocation = WindowStartupLocation.Manual,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.CanResizeWithGrip,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            Topmost = _stickyNoteTopmost,
            Content = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Child = layout
            }
        };
        CopyModalResources(stickyWindow);
        if (stickyWindow.Content is Border stickyBorder)
        {
            stickyBorder.SetResourceReference(Border.BackgroundProperty, "PanelBrush");
            stickyBorder.SetResourceReference(Border.BorderBrushProperty, "LineBrush");
        }

        var workArea = SystemParameters.WorkArea;
        var offset = _stickyNotes.Count * 24;
        stickyWindow.Left = memo.StickyLeft ?? Math.Max(workArea.Left, Left - stickyWindow.Width - 12 - offset);
        stickyWindow.Top = memo.StickyTop ?? Math.Min(workArea.Bottom - stickyWindow.Height, Math.Max(workArea.Top, Top + 40 + offset));

        titleBar.MouseLeftButtonDown += (_, args) =>
        {
            if (args.ButtonState == MouseButtonState.Pressed)
            {
                stickyWindow.DragMove();
                SaveStickyBounds(memo, stickyWindow);
            }
        };
        arrangeButton.Click += (_, _) => ArrangeStickyNotes(stickyWindow);
        addButton.Click += (_, _) => CreateStickyMemo();
        closeButton.Click += (_, _) =>
        {
            memo.IsStickyNoteEnabled = false;
            stickyWindow.Close();
        };
        stickyWindow.LocationChanged += (_, _) => SaveStickyBounds(memo, stickyWindow);
        stickyWindow.SizeChanged += (_, _) => SaveStickyBounds(memo, stickyWindow);
        stickyWindow.Closed += (_, _) =>
        {
            SaveStickyBounds(memo, stickyWindow);
            memo.IsStickyNoteOpen = false;
            if (!_isClosing && !memo.IsStickyNoteEnabled)
            {
                memo.IsStickyNoteEnabled = false;
            }
            _stickyNotes.Remove(memo.Id);
            ScheduleSave();
        };

        _stickyNotes[memo.Id] = stickyWindow;
        stickyWindow.Show();
        ApplyStickyWindowsTheme();
        SaveStickyBounds(memo, stickyWindow);

        if (focusEditor)
        {
            noteBox.Focus();
            noteBox.SelectAll();
        }
    }

    private static void SaveStickyBounds(TodoItem memo, Window window)
    {
        memo.StickyLeft = window.Left;
        memo.StickyTop = window.Top;
        memo.StickyWidth = window.Width;
        memo.StickyHeight = window.Height;
    }

    private void ArrangeStickyNotes(Window sourceWindow)
    {
        var windows = _stickyNotes
            .Select(pair => new { Memo = Todos.FirstOrDefault(todo => todo.Id == pair.Key), Window = pair.Value })
            .Where(pair => pair.Memo is not null)
            .ToList();

        if (windows.Count == 0)
        {
            return;
        }

        var workArea = SystemParameters.WorkArea;
        const double gap = 0;
        var width = Math.Max(sourceWindow.Width, 180);
        var height = Math.Max(sourceWindow.Height, 140);
        var horizontal = _stickyArrangeDirection != "Vertical";
        var columns = horizontal
            ? Math.Max(1, (int)Math.Floor((workArea.Width + gap) / (width + gap)))
            : Math.Max(1, (int)Math.Ceiling((double)windows.Count / Math.Max(1, (int)Math.Floor((workArea.Height + gap) / (height + gap)))));
        var rows = horizontal
            ? Math.Max(1, (int)Math.Ceiling((double)windows.Count / columns))
            : Math.Max(1, (int)Math.Floor((workArea.Height + gap) / (height + gap)));
        var startLeft = Math.Max(workArea.Left, Left - ((width + gap) * Math.Min(columns, windows.Count)) - gap);
        var startTop = Math.Max(workArea.Top, Top);

        for (var index = 0; index < windows.Count; index++)
        {
            var column = horizontal ? index % columns : index / rows;
            var row = horizontal ? index / columns : index % rows;
            var window = windows[index].Window;
            window.Width = width;
            window.Height = height;
            window.Left = startLeft + column * (width + gap);
            window.Top = startTop + row * (height + gap);

            if (windows[index].Memo is { } memo)
            {
                SaveStickyBounds(memo, window);
            }
        }

        ScheduleSave();
    }
    private void CreateStickyMemo()
    {
        var memo = new TodoItem
        {
            Title = string.Empty,
            IsMemo = true,
            IsStickyNoteEnabled = true,
            CreatedAt = DateTimeOffset.Now
        };

        Todos.Insert(0, memo);
        _activeFilter = "Memo";
        _showMemoTrash = false;
        UpdateCount();
        RefreshFilter();
        ApplyFilterButtonState();
        ShowStickyNote(memo, focusEditor: true);
    }
    private void PinMemo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: TodoItem memo } || !memo.IsMemo)
        {
            return;
        }

        memo.IsPinned = !memo.IsPinned;
        if (memo.IsPinned)
        {
            var index = Todos.IndexOf(memo);
            if (index > 0)
            {
                Todos.Move(index, 0);
            }
        }

        ScheduleSave();
        RefreshFilter();
    }

    private void TrashMemo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: TodoItem memo } || !memo.IsMemo)
        {
            return;
        }

        DisableStickyNote(memo);
        memo.IsDeleted = true;
        memo.IsEditingTitle = false;
        ScheduleSave();
        UpdateCount();
        RefreshFilter();
    }

    private void RestoreMemo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: TodoItem memo } || !memo.IsMemo)
        {
            return;
        }

        memo.IsDeleted = false;
        memo.IsStickyNoteEnabled = true;
        ShowStickyNote(memo, focusEditor: false);
        ScheduleSave();
        UpdateCount();
        RefreshFilter();
    }

    private void DisableStickyNote(TodoItem memo)
    {
        memo.IsStickyNoteEnabled = false;
        memo.IsStickyNoteOpen = false;

        if (_stickyNotes.TryGetValue(memo.Id, out var stickyWindow))
        {
            stickyWindow.Close();
        }
    }

    private void DeleteMemoPermanently_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: TodoItem memo } || !memo.IsMemo)
        {
            return;
        }

        UnsubscribeTodo(memo);
        Todos.Remove(memo);
        ScheduleSave();
        UpdateCount();
        RefreshFilter();
    }

    private void MemoTrashToggle_Click(object sender, RoutedEventArgs e)
    {
        _showMemoTrash = !_showMemoTrash;
        RefreshFilter();
        ApplyFilterButtonState();
        UpdateCount();
    }

    private void EmptyMemoTrash_Click(object sender, RoutedEventArgs e)
    {
        foreach (var memo in Todos.Where(todo => todo.IsMemo && todo.IsDeleted).ToList())
        {
            UnsubscribeTodo(memo);
            Todos.Remove(memo);
        }

        ScheduleSave();
        UpdateCount();
        RefreshFilter();
    }

    private static T? FindVisualChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T target && target.Name == name)
            {
                return target;
            }

            if (FindVisualChildByName<T>(child, name) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T target)
            {
                yield return target;
            }

            foreach (var nested in FindVisualChildren<T>(child))
            {
                yield return nested;
            }
        }
    }

    private void TodoList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsInteractiveElement((DependencyObject)e.OriginalSource))
        {
            ClearTodoDragState();
            return;
        }

        _dragStartPoint = e.GetPosition(null);
        _draggedContainer = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
        _draggedTodo = _draggedContainer?.DataContext as TodoItem;
    }

    private void TodoList_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _draggedTodo is null || _draggedContainer is null)
        {
            return;
        }

        var currentPosition = e.GetPosition(null);
        if (Math.Abs(currentPosition.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(currentPosition.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var draggedContainer = _draggedContainer;
        draggedContainer.Opacity = 0.45;
        ShowDragPreview(_draggedTodo.Title);
        TodoList.GiveFeedback += Drag_GiveFeedback;

        try
        {
            DragDrop.DoDragDrop(TodoList, _draggedTodo, DragDropEffects.Move);
        }
        finally
        {
            TodoList.GiveFeedback -= Drag_GiveFeedback;
            CloseDragPreview();
            draggedContainer.Opacity = 1;
            _draggedContainer = null;
            _draggedTodo = null;
        }
    }

    private void TodoList_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(TodoItem)))
        {
            return;
        }

        var sourceTodo = (TodoItem)e.Data.GetData(typeof(TodoItem))!;
        var targetTodo = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource)?.DataContext as TodoItem;
        if (targetTodo is null || ReferenceEquals(sourceTodo, targetTodo))
        {
            return;
        }

        if (sourceTodo.IsPinned != targetTodo.IsPinned)
        {
            return;
        }

        var sourceIndex = Todos.IndexOf(sourceTodo);
        var targetIndex = Todos.IndexOf(targetTodo);
        if (sourceIndex < 0 || targetIndex < 0)
        {
            return;
        }

        Todos.Move(sourceIndex, targetIndex);
        StatusText.Text = "저장 중...";
    }

    private void SubTodoList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsInteractiveElement((DependencyObject)e.OriginalSource))
        {
            ClearSubTodoDragState();
            return;
        }

        _dragStartPoint = e.GetPosition(null);
        _draggedSubParent = (sender as FrameworkElement)?.DataContext as TodoItem;
        _draggedSubContainer = FindAncestor<ContentPresenter>((DependencyObject)e.OriginalSource);
        _draggedSubTodo = _draggedSubContainer?.Content as TodoItem;
    }

    private void ClearTodoDragState()
    {
        _draggedContainer = null;
        _draggedTodo = null;
    }

    private void ClearSubTodoDragState()
    {
        _draggedSubContainer = null;
        _draggedSubTodo = null;
        _draggedSubParent = null;
    }

    private void SubTodoList_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _draggedSubTodo is null || _draggedSubParent is null || _draggedSubContainer is null)
        {
            return;
        }

        var currentPosition = e.GetPosition(null);
        if (Math.Abs(currentPosition.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(currentPosition.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _draggedSubContainer.Opacity = 0.45;
        ShowDragPreview(_draggedSubTodo.Title);
        ((ItemsControl)sender).GiveFeedback += Drag_GiveFeedback;

        try
        {
            DragDrop.DoDragDrop((DependencyObject)sender, _draggedSubTodo, DragDropEffects.Move);
        }
        finally
        {
            ((ItemsControl)sender).GiveFeedback -= Drag_GiveFeedback;
            CloseDragPreview();
            _draggedSubContainer.Opacity = 1;
            _draggedSubContainer = null;
            _draggedSubTodo = null;
            _draggedSubParent = null;
        }
    }

    private void SubTodoList_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(TodoItem)) || (sender as FrameworkElement)?.DataContext is not TodoItem parent)
        {
            return;
        }

        var sourceTodo = (TodoItem)e.Data.GetData(typeof(TodoItem))!;
        var targetTodo = FindAncestor<ContentPresenter>((DependencyObject)e.OriginalSource)?.Content as TodoItem;
        if (targetTodo is null || ReferenceEquals(sourceTodo, targetTodo) || !ReferenceEquals(parent, _draggedSubParent))
        {
            return;
        }

        var sourceIndex = parent.SubTasks.IndexOf(sourceTodo);
        var targetIndex = parent.SubTasks.IndexOf(targetTodo);
        if (sourceIndex < 0 || targetIndex < 0)
        {
            return;
        }

        parent.SubTasks.Move(sourceIndex, targetIndex);
        StatusText.Text = "저장 중...";
    }

    private static bool IsInteractiveElement(DependencyObject current)
    {
        while (current is not null)
        {
            if (current is ButtonBase or CheckBox or TextBox)
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T target)
            {
                return target;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void ShowDragPreview(string title)
    {
        CloseDragPreview();

        _dragPreview = new Window
        {
            Width = Math.Min(300, Math.Max(140, title.Length * 10 + 42)),
            Height = 38,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            ShowInTaskbar = false,
            ShowActivated = false,
            Topmost = true,
            IsHitTestVisible = false,
            Content = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(230, 32, 35, 43)),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(12, 8, 12, 8),
                Child = new TextBlock
                {
                    Text = title,
                    Foreground = System.Windows.Media.Brushes.White,
                    FontSize = 13,
                    TextTrimming = TextTrimming.CharacterEllipsis
                }
            }
        };

        MoveDragPreview();
        _dragPreview.Show();
    }

    private void Drag_GiveFeedback(object sender, GiveFeedbackEventArgs e)
    {
        MoveDragPreview();
        e.UseDefaultCursors = true;
        e.Handled = true;
    }

    private void MoveDragPreview()
    {
        if (_dragPreview is null || !GetCursorPos(out var point))
        {
            return;
        }

        _dragPreview.Left = point.X + 14;
        _dragPreview.Top = point.Y + 14;
    }

    private void CloseDragPreview()
    {
        if (_dragPreview is null)
        {
            return;
        }

        _dragPreview.Close();
        _dragPreview = null;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    private void ApplyTitleBarTheme(Window window)
    {
        if (!window.IsLoaded && PresentationSource.FromVisual(window) is null)
        {
            return;
        }

        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var isDark = 1;
        _ = DwmSetWindowAttribute(handle, DwmWindowAttribute.UseImmersiveDarkMode, ref isDark, sizeof(int));

        var caption = ToColorRef(_currentTheme == "Dark 2026" ? "#181818" : "#101114");
        var text = ToColorRef(_currentTheme == "Dark 2026" ? "#CCCCCC" : "#F3F6FA");
        var border = ToColorRef(_currentTheme == "Dark 2026" ? "#2B2B2B" : "#262A33");

        _ = DwmSetWindowAttribute(handle, DwmWindowAttribute.CaptionColor, ref caption, sizeof(int));
        _ = DwmSetWindowAttribute(handle, DwmWindowAttribute.TextColor, ref text, sizeof(int));
        _ = DwmSetWindowAttribute(handle, DwmWindowAttribute.BorderColor, ref border, sizeof(int));
    }

    private static int ToColorRef(string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        return color.R | (color.G << 8) | (color.B << 16);
    }

    private bool SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, DwmWindowAttribute attribute, ref int attributeValue, int attributeSize);

    private enum DwmWindowAttribute
    {
        UseImmersiveDarkMode = 20,
        BorderColor = 34,
        CaptionColor = 35,
        TextColor = 36
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePoint
    {
        public readonly int X;
        public readonly int Y;
    }


    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        _isClosing = true;
        foreach (var pair in _stickyNotes.ToList())
        {
            if (Todos.FirstOrDefault(todo => todo.Id == pair.Key) is { } memo)
            {
                memo.IsStickyNoteEnabled = true;
                SaveStickyBounds(memo, pair.Value);
            }

            pair.Value.Close();
        }

        _saveTimer.Stop();
        SaveAppSettings();
        _storage.Save(Todos);
    }
}


public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        return value is true ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}public sealed class SubItemMarginConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (values.Length < 3 || values[0] is not TodoItem item || values[2] is not Thickness margin)
        {
            return new Thickness(0);
        }

        var items = values[1] as IEnumerable<TodoItem>;
        return ReferenceEquals(items?.LastOrDefault(), item) ? new Thickness(0) : margin;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
