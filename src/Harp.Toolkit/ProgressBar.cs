namespace Harp.Toolkit;

internal static class ProgressBar
{
    public static void Update(int percent)
    {
        Console.CursorLeft = 0;
        Write(percent);
    }

    public static void Write(int percent)
    {
        const int Length = 10;
        Console.Write("[");
        var p = percent / Length;
        for (int i = 0; i < Length; i++)
        {
            Console.Write(i < p ? '■' : ' ');
        }
        Console.Write("] {0,3:##0}%", percent);
    }
}
