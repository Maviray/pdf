# Third-party notices

Maviray.Pdf includes the third-party files listed here. Each is under its own licence, not the Maviray.Pdf licence (see §6 of [LICENSE](LICENSE)).

## Noto Sans

- **Files:** `NotoSans-Regular.ttf`, `NotoSans-Bold.ttf`, `NotoSans-Italic.ttf` and `NotoSans-BoldItalic.ttf`, in [`src/Maviray.Pdf.Fonts.Default/Fonts/`](src/Maviray.Pdf.Fonts.Default/Fonts/).
- **Version:** 2.015, the static hinted TrueType fonts from the [NotoSans-v2.015 release](https://github.com/notofonts/latin-greek-cyrillic/releases/tag/NotoSans-v2.015) of the Noto project.
- **Copyright:** Copyright 2022 The Noto Project Authors (https://github.com/notofonts/latin-greek-cyrillic).
- **Licence:** SIL Open Font License, Version 1.1. The full text is in [`OFL.txt`](src/Maviray.Pdf.Fonts.Default/Fonts/OFL.txt), next to the fonts.

## Unicode Character Database

- **Files:** `UnicodeData.txt`, `CompositionExclusions.txt`, `DerivedCoreProperties.txt`, `LineBreak.txt`, `EastAsianWidth.txt`, `emoji-data.txt`, `NormalizationTest.txt` and `LineBreakTest.txt`, gzip-compressed in [`tests/Maviray.Pdf.Tests/Text/Unicode/Data/16.0.0/`](tests/Maviray.Pdf.Tests/Text/Unicode/Data/16.0.0/), with the licence beside them as `license.txt`.
- **Version:** 16.0.0, from [https://www.unicode.org/Public/16.0.0/ucd/](https://www.unicode.org/Public/16.0.0/ucd/).
- **Use:** The Unicode tables compiled into `Maviray.Pdf.dll` are generated from these files. The generated sources are in [`src/Maviray.Pdf/Text/Unicode/`](src/Maviray.Pdf/Text/Unicode/).
- **Licence:** Unicode License v3:

```text
UNICODE LICENSE V3

COPYRIGHT AND PERMISSION NOTICE

Copyright © 1991-2026 Unicode, Inc.

NOTICE TO USER: Carefully read the following legal agreement. BY
DOWNLOADING, INSTALLING, COPYING OR OTHERWISE USING DATA FILES, AND/OR
SOFTWARE, YOU UNEQUIVOCALLY ACCEPT, AND AGREE TO BE BOUND BY, ALL OF THE
TERMS AND CONDITIONS OF THIS AGREEMENT. IF YOU DO NOT AGREE, DO NOT
DOWNLOAD, INSTALL, COPY, DISTRIBUTE OR USE THE DATA FILES OR SOFTWARE.

Permission is hereby granted, free of charge, to any person obtaining a
copy of data files and any associated documentation (the "Data Files") or
software and any associated documentation (the "Software") to deal in the
Data Files or Software without restriction, including without limitation
the rights to use, copy, modify, merge, publish, distribute, and/or sell
copies of the Data Files or Software, and to permit persons to whom the
Data Files or Software are furnished to do so, provided that either (a)
this copyright and permission notice appear with all copies of the Data
Files or Software, or (b) this copyright and permission notice appear in
associated Documentation.

THE DATA FILES AND SOFTWARE ARE PROVIDED "AS IS", WITHOUT WARRANTY OF ANY
KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT OF
THIRD PARTY RIGHTS.

IN NO EVENT SHALL THE COPYRIGHT HOLDER OR HOLDERS INCLUDED IN THIS NOTICE
BE LIABLE FOR ANY CLAIM, OR ANY SPECIAL INDIRECT OR CONSEQUENTIAL DAMAGES,
OR ANY DAMAGES WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS,
WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION,
ARISING OUT OF OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THE DATA
FILES OR SOFTWARE.

Except as contained in this notice, the name of a copyright holder shall
not be used in advertising or otherwise to promote the sale, use or other
dealings in these Data Files or Software without prior written
authorization of the copyright holder.
```
