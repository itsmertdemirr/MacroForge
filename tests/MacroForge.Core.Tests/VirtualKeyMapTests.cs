using MacroForge.Core.Native;
using Xunit;

namespace MacroForge.Core.Tests;

public class VirtualKeyMapTests
{
    [Theory]
    [InlineData("ENTER", 0x0D)]
    [InlineData("enter", 0x0D)]
    [InlineData("LALT", 0xA4)]
    [InlineData("RCTRL", 0xA3)]
    [InlineData("NUMPAD5", 0x65)]
    [InlineData("OEM_MINUS", 0xBD)]
    [InlineData("A", 'A')]
    [InlineData("F5", 0x74)]
    public void Resolves_known_key_names(string name, int expectedVk)
    {
        Assert.Equal((ushort)expectedVk, VirtualKeyMap.Resolve(name));
    }

    [Fact]
    public void Resolves_VK_hex_fallback_names_produced_by_the_recorder()
    {
        // Regression test: the recorder used to emit "VK_A4" for left-Alt on machines
        // where that name wasn't in the friendly map, and playback threw ArgumentException.
        Assert.Equal((ushort)0xA4, VirtualKeyMap.Resolve("VK_A4"));
        Assert.Equal((ushort)0x08, VirtualKeyMap.Resolve("VK_08"));
    }

    [Fact]
    public void Throws_only_for_genuinely_unknown_names()
    {
        Assert.Throws<ArgumentException>(() => VirtualKeyMap.Resolve("NOT_A_REAL_KEY"));
    }
}
