using MacroForge.Core.Language;
using MacroForge.Core.Native;

namespace MacroForge.Core.Tests;

/// <summary>
/// Records every call made to it instead of touching the real keyboard/mouse. Lets tests assert
/// exactly what an interpreted script *would* have done, deterministically and without a Windows
/// desktop session.
/// </summary>
public sealed class FakeInputSimulator : IInputSimulator
{
    public List<string> Calls { get; } = new();

    public void MoveMouseTo(int x, int y) => Calls.Add($"move {x} {y}");
    public void MouseDown(MouseButton button) => Calls.Add($"mousedown {button}");
    public void MouseUp(MouseButton button) => Calls.Add($"mouseup {button}");
    public void Click(MouseButton button) => Calls.Add($"click {button}");
    public void PressKey(string keyName) => Calls.Add($"press {keyName}");
    public void HoldKey(string keyName) => Calls.Add($"keydown {keyName}");
    public void ReleaseKey(string keyName) => Calls.Add($"keyup {keyName}");
    public void Scroll(int amount) => Calls.Add($"scroll {amount}");
    public void TypeText(string text) => Calls.Add($"type {text}");
}
