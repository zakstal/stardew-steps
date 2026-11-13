using System.Text;

namespace StardewSteps.Core;

/// <summary>Turns a byte stream into newline-terminated ASCII lines (CR stripped).</summary>
public sealed class LineParser
{
    private const int MaxLineLength = 256;
    private readonly StringBuilder _partial = new();

    public IReadOnlyList<string> Feed(ReadOnlySpan<byte> chunk)
    {
        var lines = new List<string>();
        foreach (var b in chunk)
        {
            if (b == (byte)'\n')
            {
                lines.Add(_partial.ToString().TrimEnd('\r'));
                _partial.Clear();
            }
            else if (_partial.Length < MaxLineLength)
            {
                _partial.Append((char)b);
            }
        }
        return lines;
    }
}
