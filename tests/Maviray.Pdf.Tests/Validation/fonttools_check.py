"""Independent checks of fonts with fontTools, for the Maviray.Pdf tests (spec F-13).

Usage:
    fonttools_check.py dump <font>

dump prints, as JSON, the font's unitsPerEm, numGlyphs, hhea ascender and descender, OS/2 fsType, and the glyph ID
and advance width of every code point in its best cmap, leaving out code points mapped to glyph 0.

Any failure to read the font is printed to stderr, and the script exits with 1.
"""

import json
import sys

from fontTools.ttLib import TTFont


def dump(path):
    font = TTFont(path)
    hmtx = font["hmtx"]
    cmap = {}
    for code_point, glyph_name in sorted((font.getBestCmap() or {}).items()):
        glyph_id = font.getGlyphID(glyph_name)
        if glyph_id != 0:
            cmap[str(code_point)] = [glyph_id, hmtx[glyph_name][0]]

    result = {
        "unitsPerEm": font["head"].unitsPerEm,
        "numGlyphs": font["maxp"].numGlyphs,
        "ascender": font["hhea"].ascent,
        "descender": font["hhea"].descent,
        "fsType": font["OS/2"].fsType if "OS/2" in font else 0,
        "cmap": cmap,
    }
    json.dump(result, sys.stdout, ensure_ascii=True)


def main(argv):
    if len(argv) != 3 or argv[1] != "dump":
        print(__doc__, file=sys.stderr)
        return 2

    try:
        dump(argv[2])
    except Exception as error:  # any failure to read the font is this check failing
        print(f"{type(error).__name__}: {error}", file=sys.stderr)
        return 1

    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
