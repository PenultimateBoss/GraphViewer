namespace GraphViewer.BlazorApp.Services;

public sealed class GraphConsole
{
    public string Entry
    {
        get => field;
        set
        {
            field = value;
            OnEntryChange?.Invoke();
        }
    } = "";

    public event Action? OnEntryChange;

    public void Write(string? str)
    {
        Entry += str;
    }
    public void WriteLine(string? str = null)
    {
        Entry += $"{str}{Environment.NewLine}";
    }
    public void Clear()
    {
        Entry = "";
    }
}