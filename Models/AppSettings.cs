namespace TodoWpfPortable.Models;

public sealed class AppSettings
{
    public bool Topmost { get; set; } = true;

    public bool RightDock { get; set; } = true;

    public string Theme { get; set; } = "기본";

    public double TodoFontSize { get; set; } = 13;

    public double SubTodoFontSize { get; set; } = 13;

    public double MemoFontSize { get; set; } = 13;

    public double StickyNoteFontSize { get; set; } = 13;

    public bool StickyNoteTopmost { get; set; } = true;

    public double AddButtonSize { get; set; } = 16;

    public double MainSpacing { get; set; } = 3;

    public double SubSpacing { get; set; } = 26;

    public string StickyArrangeDirection { get; set; } = "Vertical";
}
