using System.Runtime.InteropServices;
namespace KiLupeDemo.Services;

public static class PredictionTextInserter
{
    public static bool KeysReleased => new[] { 0x10, 0x11, 0x12, 0x20, 0x5B, 0x5C }
        .Concat(Enumerable.Range(0x41, 26)).All(key => (GetAsyncKeyState(key) & 0x8000) == 0);

    public static bool Insert(IntPtr expectedWindow, string text)
    {
        if (text.Length == 0 || !KeysReleased || CaretContextReader.GetForegroundWindow() != expectedWindow) return false;
        var inputs = text.SelectMany(character => new[]
        {
            new Input { Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput { Scan = character, Flags = 4 } } },
            new Input { Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput { Scan = character, Flags = 6 } } }
        }).ToArray();
        // Never press Enter or use the clipboard. Partial writes are not retried.
        return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) == inputs.Length;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion
    {
        [FieldOffset(0)] public KeyboardInput Keyboard;
        [FieldOffset(0)] public MouseInput Mouse;
    }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput
    { public ushort VirtualKey, Scan; public uint Flags, Time; public UIntPtr ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput
    { public int X, Y; public uint Data, Flags, Time; public UIntPtr ExtraInfo; }
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
}
