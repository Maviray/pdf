# P1.5 Unicode data and NFC: execution log

Issue [#30](https://github.com/Maviray/pdf/issues/30). Plan: [2026-11-23-issue-30-unicode-data-nfc.md](../plans/2026-11-23-issue-30-unicode-data-nfc.md).

The branch `feature/30-unicode-data-nfc` starts from `develop` at `ca13853`, the merge of the P1 Text spec (#28).

## 2026-11-23

- **Planning.**
  - The spec's merge approved the download. The nine files came from unicode.org, all with LF line endings and headers naming 16.0.0. The plan records their sizes and checksums.
  - A throwaway script, independent of the code, read the expected values from the files: the counts, combining classes, decompositions, exclusions and composites the tests pin.
  - `NormalizationTest.txt` 16.0.0 has six parts, not the four T-04 named. Every part is checked, and T-04 now says "in every part".
- **Task 1, the data and its notice:**
  - `gzip -n -9` brought the eight files from 9.7 MB to 1.3 MB, about 13%. Git stores each `.gz` and `license.txt` byte for byte: the staged blobs equal the files.
  - **Red step:** the checksum test failed with `DirectoryNotFoundException` naming the data folder. Then it passed, 9 of 9.
  - The notices section was appended by a script that reads `license.txt`, so the licence isn't retyped. `dotnet pack` put `THIRD-PARTY-NOTICES.md` at the package root, beside `LICENSE` and `README.md`.
  - **Ruling:** a test helper `RepositoryFiles` finds the repository root for Tasks 1, 2 and 5, instead of three copies of the search. `UcdFiles` and the older `TestFonts` still search for their own folders, which the review noted.
  - Full suite: 513 tests, 486 passed and 27 skipped.
- **Task 2, the generator and the tables:**
  - The model tests failed to compile, then passed, 43 of 43, on the script's values.
  - **Red step:** an empty stand-in generator would have let the staleness test pass with nothing to compare. So the red step used the real generator: the test failed naming the missing `NfcPropertyData.cs`, and the folder check failed because the folder was missing.
  - The first tables didn't compile: `<<` in a generated XML comment isn't valid XML. The writer now escapes every summary. A library that doesn't compile also stops the test project, so the broken tables had to be deleted before regenerating. `CONTRIBUTING.md` says so.
  - **Ruling:** `NfcPropertyData.BlockIndex` holds bytes, not `ushort`s. 16.0.0 has 114 distinct blocks, and the generator refuses more than 256.
  - The three tables hold 75,708 bytes of data: the property blocks 29,184, their index 8,704, the decompositions 26,288 and the composites 11,532.
  - A one-character edit of `CompositionData.cs` made the staleness test fail, naming the byte where it differs. Regeneration restored the file.
- **Task 3, the lookups and Hangul:**
  - The Hangul tests, 17, use the Unicode Standard 3.12's values, U+D4DB among them.
  - The lookups agree with the model for every code point from 0 to U+10FFFF and for every composite. Values that aren't code points get the defaults.
- **Task 4, the normalizer:**
  - The first run passed 33 of 34 tests. `NfcTextAllocatesNothing` measured 720 bytes for `q` U+0308.
    - A throwaway probe measured each step over 10,000 calls. In Debug, each read of a `ushort`, `int` or `ulong` table allocated 72 bytes, where a `byte` table allocated nothing. In Release, every step, `ToNfc` included, allocated 0 bytes. Without optimization, the JIT doesn't expand `RuntimeHelpers.CreateSpan`.
    - **Ruling:** the test skips, with that reason, when the library was built without optimization. CI and the package use Release.
  - **The conformance test failed on 10 lines**, all in part 5, "Chained primary composites", new in Unicode 16.0.
    - Every failing line came back unchanged. The quick check had passed text such as U+1138B U+113C7 as NFC. But U+113C7 decomposes to U+113C2 U+113B8, and U+1138B U+113C2 composes to U+1138E.
    - A composite whose full decomposition starts with a code point that can join the character before it can join that character too. The plan's derivation missed that, and so did the throwaway script that gave its counts.
    - **Ruling:** the quick check marks those 12 composites Maybe: U+113C5, U+113C7 and U+113C8 (Tulu-Tigalari), U+16121 to U+16128 (Gurung Khema) and U+16D68 (Kirat Rai). The plan's count of Maybe code points is corrected from 120 to 132.
    - The model test, with the new count and cases, failed first and then passed.
  - After the fix, all 19,965 lines pass, and every other code point comes back unchanged. Two chained composites are also pinned in `ComposesAndReorders`.
  - **Slip:** one edit of the throwaway probe went through a shell here-document, which turned a `̈` escape into the character itself. The probe was deleted, and the rule from the P0.1 log stands: C# with backslashes goes through the file tools.
  - Full suite: 626 tests, 598 passed and 28 skipped. The 28th skip is the allocation test, in Debug.
- **Task 5, the ICU ban, both modes and CI:**
  - Both rule tests passed on the clean source. Each failed on a temporary mutant, `"a".Normalize()` and a comment naming `CultureInfo`, naming the file and line. The messages now list every match in full.
  - The globalization-mode test expects .NET's normalization to compose without the variable and to do nothing with it. It passed both ways, so each run is in the mode it claims.
  - The text tests ran 127 in each mode. The full suite is 629, which is 502 before this work plus those 127, so the two namespace filters catch every text test.
  - Release, full suite: 629 tests, 602 passed and 27 skipped (the validators, which aren't installed locally).
- **Whole-branch review** by a fresh reviewer, which was told not to download or install anything: merge with fixes, none of them critical.
  - **An independent oracle.** Python 3.14.7, already installed, carries `unicodedata` for Unicode 16.0.0, which is built from Unicode's own `DerivedNormalizationProps.txt`. It passes the committed `NormalizationTest.txt` itself.
    - The tables agree with it for all 1,114,112 code points: the combining classes, and the quick check (1,112,860 Yes, 132 Maybe, 1,120 No). So the chained-composite fix makes the quick check complete for 16.0.
    - `ToNfc` agreed with it on 300,000 random strings, and on 208 long ones that make the buffer grow. Every string that came back unchanged was the same instance.
    - In Release, a megabyte of NFC Bengali, which goes through the slow path, allocated nothing.
  - It judged all four rulings sound.
  - **Fixed, important:** `CONTRIBUTING.md` advised deleting tables that don't compile. The lookups now need them, so nothing would build. It now describes changing a table's shape in steps that each compile.
  - **Fixed, graded up from minor:**
    - **Raw characters.** The decomposed inputs in `TextNormalizerTests` and `GlobalizationModeTests` were raw combining characters: the escapes typed into the file tools had arrived as the characters themselves. A tool that normalizes a file on save would have turned those tests into identity checks that still pass.
      - A script escaped the 60 characters, building each backslash with `chr(92)`.
      - `TextTestsSpellNonAsciiAsEscapes` now fails on any raw character beyond ASCII in the text tests. It failed first, naming every raw line.
    - **Buffer growth.** No test reached the normalizer's buffer growth, although ordinary text does: one decomposed letter before a long run of precomposed ones. `GrowsItsBufferForLongExpansions` fails on a mutant that drops the copy when the buffer grows.
    - **The ICU ban** could pass while scanning nothing, in a clone under a folder named `bin` or `obj`, and it missed `TryNormalize`, `GetNormalizedLength` and `NormalizationForm`.
      - It now skips `bin` and `obj` only inside the repository, and fails if it didn't scan the file it guards. A `TryNormalize(` mutant failed it, and so did a mutant that skips every file.
  - **Full suite:** Debug 631 tests, 603 passed and 28 skipped; Release 631 tests, 604 passed and 27 skipped. In invariant mode the text tests passed 129 of 129.
  - **Deferred, minor:**
    - one Maybe character sends the rest of the text through the slow path, about 70 times slower than the fast path on NFC Bengali (30.8 ms per MB), though correct, linear and allocation-free;
    - the conformance test could also check that every c2 and c4 field comes back as the same instance;
    - `FirstNonTrivial` (U+0300) and `MaxDecompositionLength` (4) are typed by hand, where the generator could check them;
    - `UcdFiles` and `TestFonts` keep their own upward search.
