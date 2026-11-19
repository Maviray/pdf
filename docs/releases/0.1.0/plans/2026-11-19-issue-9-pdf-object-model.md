# P0.1 PDF object model and serialization: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every PDF object type, and the buffered output it writes to, serializes to the exact bytes the PDF specification requires.

**Architecture:** An abstract `PdfObject` with one sealed subclass per PDF type, each writing itself straight into a `PdfOutput`. `PdfOutput` buffers, counts bytes and never seeks. One `PdfNumberFormat` formats every real number. Everything is `internal` in `Maviray.Pdf.Writer`, and the tests reach it through `InternalsVisibleTo`.

**Tech Stack:** .NET 10 (`net10.0`), C#, xUnit v3 on Microsoft.Testing.Platform.

**Spec:** [P0 Foundations](../specs/2026-11-19-p0-foundations-design.md), decisions D-01 to D-04. Work item [#9](https://github.com/Maviray/pdf/issues/9).

## Global Constraints

- All new types are `internal`, in the namespace `Maviray.Pdf.Writer`, under `src/Maviray.Pdf/Writer/` (D-01). Tests go in `tests/Maviray.Pdf.Tests/Writer/`, namespace `Maviray.Pdf.Tests.Writer`.
- One type per file, named after the type. No partial classes.
- No package references in `src/`. `CoreAssemblyTests` keeps passing.
- No reflection and no dynamic code. The build treats warnings as errors.
- Expected bytes in tests use explicit `\n`, never multi-line string literals (D-11).
- Every line the writer emits ends with LF alone (D-05).
- Tests first: every test is seen failing (a compile error counts) before the code that makes it pass.
- **No commits while tasks run.** Each task ends with a checkpoint run. The change set is committed at the end, on the maintainer's instruction, one commit per task if approved.

## Review Focus

1. **Names with non-ASCII characters.** `Grüße` must write each UTF-8 byte as `#XX`: `/Gr#C3#BC#C3#9Fe`. Pinned in Task 4.
2. **Writes larger than the buffer.** A single write bigger than the buffer, and many small writes that cross its edge, must reach the stream complete and in order. Pinned in Task 1.
3. **Extreme integers.** `long.MinValue` and `long.MaxValue` must write all their digits with no group separators. Pinned in Task 3.
4. **Nested containers.** Arrays inside arrays and dictionaries inside dictionaries must keep single-space separators and no extra spaces at the brackets. Pinned in Task 6.
5. **Use after dispose.** Writing to a disposed `PdfOutput` must throw `ObjectDisposedException`, not write into a returned pool array. Pinned in Task 1.

---

### Task 1: `InternalsVisibleTo` and `PdfOutput`

**Files:**
- Modify: `src/Maviray.Pdf/Maviray.Pdf.csproj`: add `<ItemGroup><InternalsVisibleTo Include="Maviray.Pdf.Tests" /></ItemGroup>`
- Create: `src/Maviray.Pdf/Writer/PdfOutput.cs`
- Create: `tests/Maviray.Pdf.Tests/Writer/NonSeekableStream.cs`
- Test: `tests/Maviray.Pdf.Tests/Writer/PdfOutputTests.cs`

**Interfaces:**
- Produces:
  - `internal sealed class PdfOutput : IDisposable`
  - `PdfOutput(Stream destination, int bufferSize = 65536)`: rents the buffer from `ArrayPool<byte>.Shared`. Throws `ArgumentOutOfRangeException` when `bufferSize < 1`.
  - `long Position { get; }`: the bytes written so far.
  - `void WriteByte(byte value)` and `void Write(ReadOnlySpan<byte> bytes)`
  - `void WriteInteger(long value)`: invariant decimal digits.
  - `void Flush()`: writes the buffered bytes to the destination, then calls `destination.Flush()`.
  - `void Dispose()`: returns the buffer and writes nothing. It never disposes the destination.
- Test helper produced: `NonSeekableStream : Stream` wraps a `MemoryStream`. `CanSeek` is false, and `Position`, `Length`, `Seek` and `SetLength` throw `NotSupportedException`.

- [ ] **Step 1: Write the failing tests**

```csharp
public sealed class PdfOutputTests
{
    [Fact]
    public void WritesBytesAndCountsPosition()
    {
        var stream = new MemoryStream();
        using var output = new PdfOutput(stream);
        output.Write("%PDF"u8);
        output.WriteByte((byte)'\n');
        Assert.Equal(5, output.Position);
        output.Flush();
        Assert.Equal("%PDF\n"u8.ToArray(), stream.ToArray());
    }

    [Fact]
    public void ManySmallWritesCrossTheBufferEdgeInOrder()        // bufferSize: 16, 40 single bytes 0..39
    // Assert: after Flush the stream holds bytes 0..39 in order, and Position == 40

    [Fact]
    public void OneWriteLargerThanTheBufferArrivesWhole()         // bufferSize: 16, one write of 200_000 bytes (i % 251)
    // Assert: after Flush the stream holds exactly those bytes, and Position == 200_000

    [Fact]
    public void WorksWithAStreamThatCannotSeek()                  // NonSeekableStream, write 10 bytes, Flush
    // Assert: Position == 10, and the inner MemoryStream holds the 10 bytes

    [Fact]
    public void DisposeLeavesTheDestinationOpen()
    // Assert: after output.Dispose(), stream.CanWrite is true

    [Fact]
    public void WritingAfterDisposeThrows()
    // Assert.Throws<ObjectDisposedException> for Write, WriteByte, WriteInteger and Flush

    [Theory]
    [InlineData(0L, "0")]
    [InlineData(42L, "42")]
    [InlineData(-7L, "-7")]
    [InlineData(long.MinValue, "-9223372036854775808")]
    [InlineData(long.MaxValue, "9223372036854775807")]
    public void WritesIntegersAsInvariantDigits(long value, string expected)
    // Assert: the flushed bytes, read as Latin-1, equal expected
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/Maviray.Pdf.Tests -c Release -- --filter-class "Maviray.Pdf.Tests.Writer.PdfOutputTests"`
Expected: the build fails with CS0246, `PdfOutput` not found.

- [ ] **Step 3: Implement `PdfOutput` in `src/Maviray.Pdf/Writer/PdfOutput.cs`, add `InternalsVisibleTo`**

When a write doesn't fit in the buffer, flush the buffer first. A span larger than the whole buffer goes straight to the destination. `WriteInteger` uses `long.TryFormat` into a stack buffer with `CultureInfo.InvariantCulture`. The disposed check guards every public member.

- [ ] **Step 4: Run the tests to verify they pass**

Run: the Step 2 command. Expected: `failed: 0`, `total: 11`.

- [ ] **Step 5: Checkpoint**

Run: `dotnet build -c Release` then `dotnet test -c Release`. Expected: `0 Warning(s)`, `0 Error(s)`, and every test passes, `CoreAssemblyTests` included.

### Task 2: `PdfNumberFormat` and `PdfOutput.WriteNumber`

**Files:**
- Create: `src/Maviray.Pdf/Writer/PdfNumberFormat.cs`
- Modify: `src/Maviray.Pdf/Writer/PdfOutput.cs`: add `WriteNumber`
- Test: `tests/Maviray.Pdf.Tests/Writer/PdfNumberFormatTests.cs`

**Interfaces:**
- Consumes: `PdfOutput` (Task 1).
- Produces:
  - `internal static class PdfNumberFormat`
  - `const int MaxLength = 48`
  - `static int Format(double value, Span<byte> destination)`: returns the bytes written. Throws `ArgumentOutOfRangeException` for NaN, infinities, and `Math.Abs(value) > float.MaxValue`.
  - `PdfOutput.WriteNumber(double value)`

- [ ] **Step 1: Write the failing tests**

```csharp
public sealed class PdfNumberFormatTests
{
    [Theory]
    [InlineData(0.0, "0")]
    [InlineData(-0.0, "0")]
    [InlineData(1.0, "1")]
    [InlineData(-1.5, "-1.5")]
    [InlineData(0.5, "0.5")]
    [InlineData(595.2756, "595.2756")]
    [InlineData(841.88976, "841.8898")]
    [InlineData(1.23456, "1.2346")]
    [InlineData(0.00005, "0.0001")]
    [InlineData(0.000025, "0")]
    [InlineData(-0.00004, "0")]
    [InlineData(1e15, "1000000000000000")]
    [InlineData(123456789012.3456, "123456789012.346")]   // 15 significant digits: .NET's precision for custom formats
    public void FormatsToAtMostFourDecimals(double value, string expected)

    [Fact]
    public void LargestAllowedValueHasNoExponent()                // value = float.MaxValue
    // Assert: the result contains no 'E' or 'e', is 39 digits long, and parses back within a relative error of 1e-14

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(3.5e38)]
    [InlineData(-3.5e38)]
    public void RefusesValuesAPdfCannotHold(double value)          // Assert.Throws<ArgumentOutOfRangeException>

    [Fact]
    public void IgnoresTheCurrentCulture()
    // Set CultureInfo.CurrentCulture to de-DE in try/finally. Precondition: its NumberDecimalSeparator is ",".
    // Assert: 1.5 formats as "1.5"

    [Fact]
    public void OutputWritesTheFormattedNumber()                  // PdfOutput.WriteNumber(-12.25) writes "-12.25"
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/Maviray.Pdf.Tests -c Release -- --filter-class "Maviray.Pdf.Tests.Writer.PdfNumberFormatTests"`
Expected: the build fails with CS0103 or CS0246, `PdfNumberFormat` not found.

- [ ] **Step 3: Implement `PdfNumberFormat.Format`**

```csharp
var rounded = Math.Round(value, 4, MidpointRounding.AwayFromZero);
if (rounded == 0) rounded = 0;                       // turns -0 into 0
rounded.TryFormat(destination, out var written, "0.####", CultureInfo.InvariantCulture);
```

A custom format string without `E` never produces an exponent. It formats a double with 15 significant digits, so values of 1e11 and above get fewer than four decimals. PDF coordinates stay far below that, and the `123456789012.3456` case pins the behaviour. Checked on .NET 10 before this plan was written. Check the range before rounding.

- [ ] **Step 4: Run the tests to verify they pass**

Run: the Step 2 command. Expected: `failed: 0`, `total: 21`.

- [ ] **Step 5: Checkpoint.** Same commands and expectations as Task 1, Step 5.

### Task 3: `PdfObject` and the scalar types

**Files:**
- Create: `src/Maviray.Pdf/Writer/PdfObject.cs`, `PdfNull.cs`, `PdfBoolean.cs`, `PdfInteger.cs`, `PdfReal.cs`, `PdfReference.cs`
- Create: `tests/Maviray.Pdf.Tests/Writer/Serialized.cs`
- Test: `tests/Maviray.Pdf.Tests/Writer/PdfScalarTests.cs`

**Interfaces:**
- Consumes: `PdfOutput`, `PdfNumberFormat` (Tasks 1 and 2).
- Produces:
  - `internal abstract class PdfObject { public abstract void WriteTo(PdfOutput output); }`
  - `PdfNull.Instance`, with a private constructor.
  - `PdfBoolean.True` and `PdfBoolean.False`, with a private constructor and `bool Value`.
  - `PdfInteger(long value)`, with `long Value`.
  - `PdfReal(double value)`, with `double Value`. It throws `ArgumentOutOfRangeException` on the same inputs as `PdfNumberFormat.Format`.
  - `PdfReference(int objectNumber)`, with `int ObjectNumber`. It throws `ArgumentOutOfRangeException` when `objectNumber < 1`.
- Test helper produced: `static class Serialized` with `static string Of(PdfObject value)`. It writes the object to a `MemoryStream` through `PdfOutput`, flushes, and returns `Encoding.Latin1.GetString(bytes)`. Latin-1 maps every byte to one character, so the comparison hides nothing.

- [ ] **Step 1: Write the failing tests**

```csharp
public sealed class PdfScalarTests
{
    [Fact] public void NullWritesNull() => Assert.Equal("null", Serialized.Of(PdfNull.Instance));
    [Fact] public void TrueWritesTrue() => Assert.Equal("true", Serialized.Of(PdfBoolean.True));
    [Fact] public void FalseWritesFalse() => Assert.Equal("false", Serialized.Of(PdfBoolean.False));

    [Theory]
    [InlineData(0L, "0")]
    [InlineData(-7L, "-7")]
    [InlineData(long.MinValue, "-9223372036854775808")]
    [InlineData(long.MaxValue, "9223372036854775807")]
    public void IntegerWritesDigits(long value, string expected)

    [Theory]
    [InlineData(0.5, "0.5")]
    [InlineData(-0.0, "0")]
    [InlineData(72.0, "72")]
    public void RealWritesTheSharedNumberFormat(double value, string expected)

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(3.5e38)]
    public void RealRefusesValuesAPdfCannotHold(double value)    // Assert.Throws<ArgumentOutOfRangeException>(() => new PdfReal(value))

    [Fact] public void ReferenceWritesNumberGenerationAndR() => Assert.Equal("12 0 R", Serialized.Of(new PdfReference(12)));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ReferenceRefusesObjectNumbersBelowOne(int number) // Assert.Throws<ArgumentOutOfRangeException>
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/Maviray.Pdf.Tests -c Release -- --filter-class "Maviray.Pdf.Tests.Writer.PdfScalarTests"`
Expected: the build fails with CS0246, `PdfObject` not found.

- [ ] **Step 3: Implement the six types.** Keywords are UTF-8 literals (`"null"u8`, `" 0 R"u8`).

- [ ] **Step 4: Run the tests to verify they pass**

Run: the Step 2 command. Expected: `failed: 0`, `total: 16`.

- [ ] **Step 5: Checkpoint.** Same as Task 1, Step 5.

### Task 4: `PdfName`

**Files:**
- Create: `src/Maviray.Pdf/Writer/PdfName.cs`
- Test: `tests/Maviray.Pdf.Tests/Writer/PdfNameTests.cs`

**Interfaces:**
- Consumes: `PdfObject`, `PdfOutput`, and the `Serialized` helper.
- Produces:
  - `internal sealed class PdfName : PdfObject, IEquatable<PdfName>`
  - `PdfName(string value)`: throws `ArgumentException` when `value` is empty, and `ArgumentNullException` when it's null.
  - `string Value`. `Equals` and `GetHashCode` compare `Value` ordinally.
  - Shared instances: `static PdfName Length { get; }` and `static PdfName Filter { get; }`. Later items add the names they use.

- [ ] **Step 1: Write the failing tests**

```csharp
public sealed class PdfNameTests
{
    [Theory]
    [InlineData("Type", "/Type")]
    [InlineData("!~", "/!~")]                    // 0x21 and 0x7E are regular characters
    [InlineData("A B", "/A#20B")]
    [InlineData("a#b", "/a#23b")]
    [InlineData("\u007F", "/#7F")]
    [InlineData("(", "/#28")] [InlineData(")", "/#29")] [InlineData("<", "/#3C")] [InlineData(">", "/#3E")]
    [InlineData("[", "/#5B")] [InlineData("]", "/#5D")] [InlineData("{", "/#7B")] [InlineData("}", "/#7D")]
    [InlineData("/", "/#2F")] [InlineData("%", "/#25")]
    [InlineData("Grüße", "/Gr#C3#BC#C3#9Fe")]
    public void WritesSlashAndEscapedBytes(string value, string expected)

    [Fact] public void RefusesAnEmptyName()      // Assert.Throws<ArgumentException>(() => new PdfName(""))

    [Fact] public void NamesWithTheSameValueAreEqual()
    // Assert: new PdfName("Type").Equals(new PdfName("Type")), equal hash codes, and not equal to new PdfName("type")

    [Fact] public void SharedNamesWriteTheirValue()
    // Assert: Serialized.Of(PdfName.Length) == "/Length", and Serialized.Of(PdfName.Filter) == "/Filter"
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/Maviray.Pdf.Tests -c Release -- --filter-class "Maviray.Pdf.Tests.Writer.PdfNameTests"`
Expected: the build fails with CS0246, `PdfName` not found.

- [ ] **Step 3: Implement `PdfName`.** Encode `Value` to UTF-8 once, in the constructor. `WriteTo` writes `#` plus two uppercase hex digits for every byte that D-02 says to escape.

- [ ] **Step 4: Run the tests to verify they pass**

Run: the Step 2 command. Expected: `failed: 0`, `total: 19`.

- [ ] **Step 5: Checkpoint.** Same as Task 1, Step 5.

### Task 5: `PdfString` and `PdfStringForm`

**Files:**
- Create: `src/Maviray.Pdf/Writer/PdfStringForm.cs`, `src/Maviray.Pdf/Writer/PdfString.cs`
- Test: `tests/Maviray.Pdf.Tests/Writer/PdfStringTests.cs`

**Interfaces:**
- Consumes: `PdfObject`, `PdfOutput`, and `Serialized`.
- Produces:
  - `internal enum PdfStringForm { Literal, Hex }`
  - `internal sealed class PdfString : PdfObject`
  - `PdfString(ReadOnlyMemory<byte> bytes, PdfStringForm form = PdfStringForm.Literal)`, with `ReadOnlyMemory<byte> Bytes` and `PdfStringForm Form`.
  - `static PdfString FromText(string text)`: ASCII `0x20`–`0x7E` gives a literal string of those bytes. Anything else gives a hex string of `FE FF` followed by `Encoding.BigEndianUnicode` bytes.
  - `static void WriteLiteral(PdfOutput output, ReadOnlySpan<byte> bytes)`: writes `(…)` with the D-02 escaping. P0.4 reuses it for `Tj`.

- [ ] **Step 1: Write the failing tests**

```csharp
public sealed class PdfStringTests
{
    private static string Literal(params byte[] bytes) => Serialized.Of(new PdfString(bytes));

    [Fact] public void WritesPlainBytesInParentheses()   => Assert.Equal("(Hello)", Literal("Hello"u8.ToArray()));
    [Fact] public void EscapesParentheses()              => Assert.Equal("(a\\(b\\)c)", Literal("a(b)c"u8.ToArray()));
    [Fact] public void EscapesAnUnbalancedParenthesis()  => Assert.Equal("(\\))", Literal((byte)')'));
    [Fact] public void EscapesBackslash()                => Assert.Equal("(a\\\\b)", Literal("a\\b"u8.ToArray()));
    [Fact] public void EscapesControlCharactersByName()  => Assert.Equal("(\\r\\n\\t\\b\\f)", Literal(13, 10, 9, 8, 12));

    [Theory]
    [InlineData((byte)0x01, "(\\001)")]
    [InlineData((byte)0x1F, "(\\037)")]
    [InlineData((byte)0x7F, "(\\177)")]
    public void EscapesOtherControlBytesAsOctal(byte value, string expected)

    [Fact] public void WritesHighBytesAsIs()             => Assert.Equal("(éÿ)", Literal(0xE9, 0xFF));
    [Fact] public void WritesAnEmptyLiteral()            => Assert.Equal("()", Literal());

    [Fact] public void WritesHexInUppercasePairs()
    // Assert: new PdfString(new byte[] { 0x00, 0xAB, 0xFF }, PdfStringForm.Hex) writes "<00ABFF>", and empty hex writes "<>"

    [Theory]
    [InlineData("Hello", "(Hello)")]
    [InlineData("", "()")]
    [InlineData("Grüße", "<FEFF0047007200FC00DF0065>")]
    [InlineData("\U0001F600", "<FEFFD83DDE00>")]
    [InlineData("a\nb", "<FEFF0061000A0062>")]          // a control character isn't printable ASCII
    public void FromTextPicksAsciiOrUtf16(string text, string expected)
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/Maviray.Pdf.Tests -c Release -- --filter-class "Maviray.Pdf.Tests.Writer.PdfStringTests"`
Expected: the build fails with CS0246, `PdfString` not found.

- [ ] **Step 3: Implement `PdfStringForm` and `PdfString`.** `WriteTo` calls `WriteLiteral` for the literal form. For the hex form it writes `<`, two uppercase hex digits per byte, then `>`.

- [ ] **Step 4: Run the tests to verify they pass**

Run: the Step 2 command. Expected: `failed: 0`, `total: 16`.

- [ ] **Step 5: Checkpoint.** Same as Task 1, Step 5.

### Task 6: `PdfArray`, `PdfDictionary` and `PdfStream`

**Files:**
- Create: `src/Maviray.Pdf/Writer/PdfArray.cs`, `PdfDictionary.cs`, `PdfStream.cs`
- Test: `tests/Maviray.Pdf.Tests/Writer/PdfArrayTests.cs`, `PdfDictionaryTests.cs`, `PdfStreamTests.cs`

**Interfaces:**
- Consumes: every type from Tasks 3 to 5, and `PdfName.Length` and `PdfName.Filter`.
- Produces:
  - `internal sealed class PdfArray : PdfObject, IReadOnlyList<PdfObject>`
    - `PdfArray()`, `PdfArray(params PdfObject[] items)`, `void Add(PdfObject item)`
  - `internal sealed class PdfDictionary : PdfObject`
    - `PdfDictionary Set(PdfName key, PdfObject value)`: returns `this`. It replaces an existing key in place, and throws `ArgumentException` for a `PdfNull` value.
    - `bool Remove(PdfName key)`, `bool TryGetValue(PdfName key, out PdfObject value)`, `bool ContainsKey(PdfName key)`, `int Count`
    - Entries keep their insertion order.
  - `internal sealed class PdfStream : PdfObject`
    - `PdfStream(PdfDictionary dictionary, ReadOnlyMemory<byte> data)`: throws `ArgumentException` if the dictionary contains `/Length` or `/Filter` (D-08).
    - `PdfDictionary Dictionary` and `ReadOnlyMemory<byte> Data`
    - `WriteTo` writes the dictionary's entries followed by `/Length <data length>`, then LF, `stream`, LF, the data, LF, `endstream`. The caller's dictionary is never changed.
    - Compression is P0.2's work.

- [ ] **Step 1: Write the failing tests**

```csharp
public sealed class PdfArrayTests
{
    [Fact] public void WritesAnEmptyArray() => Assert.Equal("[]", Serialized.Of(new PdfArray()));

    [Fact] public void SeparatesItemsWithSingleSpaces()
    // [new PdfInteger(1), new PdfName("Name"), new PdfString("s"u8.ToArray())] writes "[1 /Name (s)]"

    [Fact] public void NestsArrays()
    // [[1 2] [3]] writes "[[1 2] [3]]"
}

public sealed class PdfDictionaryTests
{
    [Fact] public void WritesAnEmptyDictionary() => Assert.Equal("<<>>", Serialized.Of(new PdfDictionary()));

    [Fact] public void WritesEntriesInInsertionOrder()
    // Set Type /Page, then Count 1: "<</Type /Page /Count 1>>"

    [Fact] public void SettingAnExistingKeyReplacesItInPlace()
    // Set A 1, B 2, A 3: "<</A 3 /B 2>>", and Count == 2

    [Fact] public void RefusesANullValue()           // Assert.Throws<ArgumentException>(() => dict.Set(new PdfName("A"), PdfNull.Instance))

    [Fact] public void RemovesAndLooksUpKeys()
    // After Set A 1 and Remove(A): Remove returns true, ContainsKey(A) is false, TryGetValue(A) is false, and it writes "<<>>"

    [Fact] public void NestsDictionaries()
    // A -> << B -> 1 >>: "<</A <</B 1>>>>"
}

public sealed class PdfStreamTests
{
    [Fact] public void WritesDictionaryLengthAndData()
    // dictionary Type /XObject, data "hello": "<</Type /XObject /Length 5>>\nstream\nhello\nendstream"

    [Fact] public void WritesAnEmptyStream()
    // empty dictionary, no data: "<</Length 0>>\nstream\n\nendstream"

    [Fact] public void KeepsBinaryDataByteForByte()
    // data = bytes 0..255. Assert: the Latin-1 text between "stream\n" and "\nendstream" equals those 256 bytes

    [Fact] public void LeavesTheCallersDictionaryUnchanged()
    // Assert: after Serialized.Of(stream), dictionary.ContainsKey(PdfName.Length) is false

    [Theory]
    [InlineData("Length")]
    [InlineData("Filter")]
    public void RefusesADictionaryThatSetsLengthOrFilter(string key) // Assert.Throws<ArgumentException>
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/Maviray.Pdf.Tests -c Release -- --filter-namespace "Maviray.Pdf.Tests.Writer"`
Expected: the build fails with CS0246, `PdfArray` not found.

- [ ] **Step 3: Implement the three types.** `PdfDictionary` stores a `List<KeyValuePair<PdfName, PdfObject>>` and searches it in a line, because dictionaries are small. It exposes an `internal void WriteEntries(PdfOutput output)`, which `PdfStream` uses to add `/Length` without copying.

- [ ] **Step 4: Run the tests to verify they pass**

Run: the Step 2 command. Expected: `failed: 0`. Every `Maviray.Pdf.Tests.Writer` test passes.

- [ ] **Step 5: Checkpoint.** Same as Task 1, Step 5.

### Task 7: Records and hand-back

**Files:**
- Create: `docs/releases/0.1.0/execution-logs/2026-11-19-issue-9-pdf-object-model.md`
- Modify: `docs/releases/0.1.0/README.md`

- [ ] **Step 1: Write the execution log.** List each task, the date, the test count it added, and any departure from this plan and why. Append to it while the tasks run, not afterwards.

- [ ] **Step 2: Update the release index.**
  - The P0.1 row becomes `[#9](https://github.com/Maviray/pdf/issues/9)` with the status "In review, YYYY-MM-DD", and links to this plan and the log.
  - The P0 spec row becomes "Approved, 2026-11-19".
  - The other P0 rows link their issues, #10 to #13.
  - The Layout section adds `plans/` and `execution-logs/`.

- [ ] **Step 3: Verify the change set.**
  - Run `dotnet build -c Release` and `dotnet test -c Release`. Expected: no warnings, no errors, every test passing.
  - Run `git status --short` and `git diff --name-only`. Expected: only `src/Maviray.Pdf/Maviray.Pdf.csproj`, `src/Maviray.Pdf/Writer/*`, `tests/Maviray.Pdf.Tests/Writer/*` and the three docs files above.

- [ ] **Step 4: Hand back.** Report the result and the test counts. Propose the commits, one per task, with subjects `#9 <imperative summary>`. Wait for the instruction to commit, push and open the PR.
