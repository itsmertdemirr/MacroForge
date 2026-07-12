using MacroForge.Core.Language;
using MacroForge.Core.Native;

namespace MacroForge.Benchmarks;

/// <summary>Does nothing — isolates the interpreter's own dispatch/loop overhead from the cost
/// of actually calling into Win32 SendInput, which would dominate and mask everything else.</summary>
internal sealed class NoOpInputSimulator : IInputSimulator
{
    public void MoveMouseTo(int x, int y) { }
    public void MouseDown(MouseButton button) { }
    public void MouseUp(MouseButton button) { }
    public void Click(MouseButton button) { }
    public void PressKey(string keyName) { }
    public void HoldKey(string keyName) { }
    public void ReleaseKey(string keyName) { }
    public void Scroll(int amount) { }
    public void TypeText(string text) { }
}
