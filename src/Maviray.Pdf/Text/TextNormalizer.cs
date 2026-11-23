using System.Buffers;
using System.Text;

namespace Maviray.Pdf.Text;

/// <summary>
/// Cleans and normalizes input text (spec T-03 and T-04): each unpaired surrogate becomes U+FFFD, and the text is put
/// in Normalization Form C by the algorithm of the Unicode Standard 3.11, behind the quick check of UAX #15. .NET's own
/// normalization isn't used, because it depends on ICU and does nothing in invariant-globalization mode.
/// </summary>
internal static class TextNormalizer
{
    private const int ReplacementCharacter = 0xFFFD;

    /// <summary>The lowest code point that has a combining class or a quick-check value other than Yes.</summary>
    private const int FirstNonTrivial = 0x0300;

    /// <summary>
    /// Returns the NFC form of <paramref name="text"/>, with each unpaired surrogate replaced by U+FFFD. Text that is
    /// already NFC and has no unpaired surrogate is returned as the same instance, and nothing is allocated for it.
    /// </summary>
    public static string ToNfc(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var start = FindStart(text);
        return start < 0 ? text : ComposeFrom(text, start);
    }

    /// <summary>
    /// The quick check. Returns −1 when the text can't change. Otherwise returns where the work must start: the last
    /// starter before the first code point that may change, or 0. A boundary before a starter whose quick check is
    /// Yes is safe, because nothing after it can compose with or reorder across it.
    /// </summary>
    private static int FindStart(string text)
    {
        var lastStarter = 0;
        var lastClass = 0;
        for (var i = 0; i < text.Length;)
        {
            int c = text[i];
            if (c < FirstNonTrivial)
            {
                lastStarter = i;
                lastClass = 0;
                i++;
                continue;
            }

            var width = 1;
            if (char.IsSurrogate((char)c))
            {
                if (!char.IsHighSurrogate((char)c) || i + 1 >= text.Length || !char.IsLowSurrogate(text[i + 1]))
                {
                    return lastStarter;
                }

                c = char.ConvertToUtf32((char)c, text[i + 1]);
                width = 2;
            }

            var combiningClass = NormalizationLookup.CombiningClass(c);
            if ((combiningClass != 0 && lastClass > combiningClass) || NormalizationLookup.QuickCheck(c) != NfcQuickCheck.Yes)
            {
                return lastStarter;
            }

            if (combiningClass == 0)
            {
                lastStarter = i;
            }

            lastClass = combiningClass;
            i += width;
        }

        return -1;
    }

    /// <summary>Decomposes, reorders and recomposes the text from <paramref name="start"/> to its end.</summary>
    private static string ComposeFrom(string text, int start)
    {
        var buffer = ArrayPool<int>.Shared.Rent(text.Length - start + 16);
        try
        {
            var count = Decompose(text, start, ref buffer);
            SortMarks(buffer, count);
            count = Compose(buffer, count);
            return SameCodePoints(text, start, buffer, count) ? text : Build(text, start, buffer, count);
        }
        finally
        {
            ArrayPool<int>.Shared.Return(buffer);
        }
    }

    /// <summary>Writes the full canonical decomposition of each code point, a lone surrogate as U+FFFD.</summary>
    private static int Decompose(string text, int start, ref int[] buffer)
    {
        Span<int> mapping = stackalloc int[NormalizationLookup.MaxDecompositionLength];
        var count = 0;
        for (var i = start; i < text.Length;)
        {
            var c = ReadCodePoint(text, i, out var width, replaceLoneSurrogates: true);
            i += width;

            var length = NormalizationLookup.Decompose(c, mapping);
            if (count + length > buffer.Length)
            {
                var larger = ArrayPool<int>.Shared.Rent(Math.Max(buffer.Length * 2, count + length));
                buffer.AsSpan(0, count).CopyTo(larger);
                ArrayPool<int>.Shared.Return(buffer);
                buffer = larger;
            }

            mapping[..length].CopyTo(buffer.AsSpan(count));
            count += length;
        }

        return count;
    }

    /// <summary>
    /// The canonical ordering: a stable sort of each run of non-starters by combining class. A run that is out of
    /// order gets a counting sort, so a long run costs time in proportion to its length.
    /// </summary>
    private static void SortMarks(int[] buffer, int count)
    {
        for (var i = 0; i < count;)
        {
            if (NormalizationLookup.CombiningClass(buffer[i]) == 0)
            {
                i++;
                continue;
            }

            var runStart = i;
            var sorted = true;
            var previous = 0;
            while (i < count)
            {
                var combiningClass = NormalizationLookup.CombiningClass(buffer[i]);
                if (combiningClass == 0)
                {
                    break;
                }

                sorted &= combiningClass >= previous;
                previous = combiningClass;
                i++;
            }

            if (!sorted)
            {
                CountingSort(buffer.AsSpan(runStart, i - runStart));
            }
        }
    }

    private static void CountingSort(Span<int> run)
    {
        Span<int> positions = stackalloc int[256];
        positions.Clear();
        foreach (var c in run)
        {
            positions[NormalizationLookup.CombiningClass(c)]++;
        }

        var position = 0;
        for (var combiningClass = 0; combiningClass < positions.Length; combiningClass++)
        {
            var size = positions[combiningClass];
            positions[combiningClass] = position;
            position += size;
        }

        var sorted = ArrayPool<int>.Shared.Rent(run.Length);
        try
        {
            foreach (var c in run)
            {
                sorted[positions[NormalizationLookup.CombiningClass(c)]++] = c;
            }

            sorted.AsSpan(0, run.Length).CopyTo(run);
        }
        finally
        {
            ArrayPool<int>.Shared.Return(sorted);
        }
    }

    /// <summary>
    /// The canonical composition, in place. A code point joins the last starter when the pair has a primary composite
    /// and nothing blocks it: it follows the starter directly, or every code point between them has a lower class.
    /// The code points between a starter and the next are non-starters in ascending class order, so the last one kept
    /// has the highest class among them.
    /// </summary>
    private static int Compose(int[] buffer, int count)
    {
        var starter = -1;
        var lastClass = 0;
        var kept = 0;
        for (var i = 0; i < count; i++)
        {
            var c = buffer[i];
            var combiningClass = NormalizationLookup.CombiningClass(c);
            if (starter >= 0 && (kept - 1 == starter || lastClass < combiningClass))
            {
                var composite = NormalizationLookup.Compose(buffer[starter], c);
                if (composite >= 0)
                {
                    buffer[starter] = composite;
                    continue;
                }
            }

            if (combiningClass == 0)
            {
                starter = kept;
            }

            lastClass = combiningClass;
            buffer[kept++] = c;
        }

        return kept;
    }

    private static bool SameCodePoints(string text, int start, int[] buffer, int count)
    {
        var k = 0;
        for (var i = start; i < text.Length;)
        {
            var c = ReadCodePoint(text, i, out var width, replaceLoneSurrogates: false);
            i += width;
            if (k == count || buffer[k++] != c)
            {
                return false;
            }
        }

        return k == count;
    }

    private static string Build(string text, int start, int[] buffer, int count)
    {
        var length = start;
        for (var k = 0; k < count; k++)
        {
            length += buffer[k] >= 0x10000 ? 2 : 1;
        }

        return string.Create(length, (text, start, buffer, count), static (span, state) =>
        {
            state.text.AsSpan(0, state.start).CopyTo(span);
            var position = state.start;
            for (var k = 0; k < state.count; k++)
            {
                position += new Rune(state.buffer[k]).EncodeToUtf16(span[position..]);
            }
        });
    }

    /// <summary>
    /// Reads the code point at <paramref name="index"/>. A lone surrogate is U+FFFD when
    /// <paramref name="replaceLoneSurrogates"/> is set, and its own code unit otherwise.
    /// </summary>
    private static int ReadCodePoint(string text, int index, out int width, bool replaceLoneSurrogates)
    {
        var c = text[index];
        width = 1;
        if (!char.IsSurrogate(c))
        {
            return c;
        }

        if (char.IsHighSurrogate(c) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
        {
            width = 2;
            return char.ConvertToUtf32(c, text[index + 1]);
        }

        return replaceLoneSurrogates ? ReplacementCharacter : c;
    }
}
