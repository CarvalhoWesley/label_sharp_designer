using LabelSharpDesigner.Core.Document;

namespace LabelSharpDesigner.History;

public abstract record DocumentCommand : ICommand
{
    public abstract string Description { get; }

    public required LabelDocument Before { get; init; }

    public required LabelDocument After { get; init; }
}
