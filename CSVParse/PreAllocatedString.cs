using System.Diagnostics.CodeAnalysis;

namespace CSVParse;

/// <summary>
/// Represents a string which uses pre-allocated memory.
/// </summary>
public struct PreAllocatedString
{
    readonly internal char[] _array;
    public Memory<char> data;

    public readonly Span<char> Span => data.Span;
    public readonly int Length => data.Length;
    public readonly bool IsEmpty => data.IsEmpty;

    /// <summary>
    /// Creates and allocates storage for this string.
    /// </summary>
    /// <param name="capacity">The number of chars of capacity to allocate.</param>
    public PreAllocatedString(int capacity)
    {
        _array = new char[capacity];
        data = new Memory<char>(_array, 0, 0);
    }

    /// <summary>
    /// Copies the provided span of chars into this string.
    /// </summary>
    /// <param name="chars"></param>
    public void Update(ReadOnlySpan<char> chars)
    {
        chars.CopyTo(_array);
        data = new Memory<char>(_array, 0, chars.Length);
    }

    public readonly override string ToString()
    {
        return new(data.Span);
    }

    public override readonly bool Equals([NotNullWhen(true)] object? obj)
    {
        if (obj is PreAllocatedString str)
            Equals(str);

        return base.Equals(obj);
    }

    public readonly bool Equals(PreAllocatedString other)
    {
        return data.Span.SequenceEqual(other.data.Span);
    }

    public static bool operator ==(PreAllocatedString left, PreAllocatedString right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(PreAllocatedString left, PreAllocatedString right)
    {
        return !(left == right);
    }

    public override readonly int GetHashCode()
    {
        return string.GetHashCode(data.Span);
    }
}
