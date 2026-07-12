using MacroForge.Core.Language;

namespace MacroForge.Core.Native;

/// <summary>
/// Abstraction over <see cref="InputSimulator"/>. Exists so <see cref="MacroForge.Core.Interpreter"/>
/// can be unit-tested (including cancellation/pause/cleanup behaviour) without touching the real
/// keyboard/mouse or requiring a Windows desktop session — tests substitute a fake implementation
/// that just records which calls were made.
/// </summary>
public interface IInputSimulator
{
    void MoveMouseTo(int x, int y);
    void MouseDown(MouseButton button);
    void MouseUp(MouseButton button);
    void Click(MouseButton button);
    void PressKey(string keyName);
    void HoldKey(string keyName);
    void ReleaseKey(string keyName);
    void Scroll(int amount);
    void TypeText(string text);
}
