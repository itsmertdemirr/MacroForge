namespace MacroForge.Core.Language;

/// <summary>Base type for every statement in a parsed MacroForge script.</summary>
public abstract class Statement
{
    public int Line { get; init; }
}

public sealed class WaitStatement : Statement
{
    public required int Milliseconds { get; init; }
}

public sealed class MouseMoveStatement : Statement
{
    public required int X { get; init; }
    public required int Y { get; init; }
}

public enum MouseButton { Left, Right, Middle }

public sealed class MouseClickStatement : Statement
{
    public required MouseButton Button { get; init; }
}

public sealed class MouseDownStatement : Statement
{
    public required MouseButton Button { get; init; }
}

public sealed class MouseUpStatement : Statement
{
    public required MouseButton Button { get; init; }
}

public sealed class KeyPressStatement : Statement
{
    public required string Key { get; init; }
}

public sealed class KeyDownStatement : Statement
{
    public required string Key { get; init; }
}

public sealed class KeyUpStatement : Statement
{
    public required string Key { get; init; }
}

public sealed class KeyTypeStatement : Statement
{
    public required string Text { get; init; }
}

public sealed class MouseScrollStatement : Statement
{
    public required int Amount { get; init; }
}

public sealed class RepeatStatement : Statement
{
    public required int Count { get; init; }
    public required IReadOnlyList<Statement> Body { get; init; }
}

/// <summary>The root of a parsed script: an ordered list of top-level statements.</summary>
public sealed class MacroScript
{
    public required IReadOnlyList<Statement> Statements { get; init; }
}
