namespace GraphViewer.BlazorApp.Services;

public sealed partial class ControlConsole
{
    public string ConsoleEntry
    {
        get => field;
        set
        {
            field = value;
            EntryChanged?.Invoke();
        }
    } = "";

    public event Action? EntryChanged;

    public void Write(string str)
    {
        ConsoleEntry += str;
    }
    public void WriteLine()
    {
        Write(Environment.NewLine);
    }
    public void WriteLine(string str)
    {
        Write($"{str}{Environment.NewLine}");
    }
    public void Clear()
    {
        ConsoleEntry = "";
    }
}