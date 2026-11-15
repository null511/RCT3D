namespace RCT3D.Render;

internal abstract record ViewerCommand
{
    public sealed record LoadPark(string Path) : ViewerCommand;
    public sealed record ReturnToMenu : ViewerCommand;
    public sealed record Exit : ViewerCommand;
}

internal sealed class ViewerCommandQueue
{
    private readonly Queue<ViewerCommand> commands = new();

    public void LoadPark(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        commands.Enqueue(new ViewerCommand.LoadPark(path));
    }

    public void ReturnToMenu() => commands.Enqueue(new ViewerCommand.ReturnToMenu());
    public void Exit() => commands.Enqueue(new ViewerCommand.Exit());
    public bool TryDequeue(out ViewerCommand? command) => commands.TryDequeue(out command);
}
