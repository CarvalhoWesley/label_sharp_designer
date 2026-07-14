namespace LabelSharpDesigner.History;

/// <summary>Bundles several commands into a single undo/redo step.</summary>
public sealed record CompositeCommand : DocumentCommand
{
    public required IReadOnlyList<ICommand> Commands { get; init; }

    public override string Description => Commands.Count == 1
        ? Commands[0].Description
        : $"{Commands.Count} alterações";

    public static CompositeCommand From(IReadOnlyList<ICommand> commands) => new()
    {
        Commands = commands,
        Before = commands[0].Before,
        After = commands[^1].After,
    };
}
