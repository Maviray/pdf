"""Independent checks of fonts with fontTools, for the Maviray.Pdf tests (spec F-13).

Usage:
    fonttools_check.py dump <font>
    fonttools_check.py subset <original font> <subset> <glyph ids file>

dump prints, as JSON, the font's unitsPerEm, numGlyphs, hhea ascender and descender, OS/2 fsType, and the glyph ID
and advance width of every code point in its best cmap, leaving out code points mapped to glyph 0.

subset loads the subset with checksum errors raised and decompiles every table. The ids file holds, space-separated,
the original glyph ID of each subset glyph. Every subset glyph is compared with its original: the contour count, the
outline with components resolved, the components and their transforms, the instructions and the hmtx entry. maxp
numGlyphs and hhea numberOfHMetrics must equal the number of IDs. It prints {"glyphs": n}.

Any failure is printed to stderr, and the script exits with 1.
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


def outline(glyph, glyf):
    coordinates, end_points, flags = glyph.getCoordinates(glyf)
    return list(coordinates), list(end_points), list(flags)


def instructions(glyph):
    program = getattr(glyph, "program", None)
    return program.getBytecode() if program is not None else b""


def attributes(component):
    return {key: value for key, value in vars(component).items() if key != "glyphName"}


def check_subset(original_path, subset_path, ids_path):
    with open(ids_path, encoding="ascii") as ids_file:
        ids = [int(token) for token in ids_file.read().split()]

    original = TTFont(original_path)
    subset = TTFont(subset_path, checkChecksums=2)
    for tag in subset.keys():
        subset[tag]  # decompiles the table; a wrong checksum raises here

    problems = []
    if subset["maxp"].numGlyphs != len(ids):
        problems.append(f"maxp.numGlyphs is {subset['maxp'].numGlyphs}, expected {len(ids)}")
    if subset["hhea"].numberOfHMetrics != len(ids):
        problems.append(f"hhea.numberOfHMetrics is {subset['hhea'].numberOfHMetrics}, expected {len(ids)}")

    original_order = original.getGlyphOrder()
    subset_order = subset.getGlyphOrder()
    subset_index = {name: index for index, name in enumerate(subset_order)}
    original_glyf, subset_glyf = original["glyf"], subset["glyf"]
    original_hmtx, subset_hmtx = original["hmtx"], subset["hmtx"]
    for index, original_id in enumerate(ids[: len(subset_order)]):
        theirs_name, ours_name = original_order[original_id], subset_order[index]
        theirs, ours = original_glyf[theirs_name], subset_glyf[ours_name]
        where = f"subset glyph {index} (original {original_id})"
        if theirs.numberOfContours != ours.numberOfContours:
            problems.append(f"{where}: {ours.numberOfContours} contours, expected {theirs.numberOfContours}")
            continue
        if theirs.isComposite():
            expected = [(c.glyphName, attributes(c)) for c in theirs.components]
            actual = [(original_order[ids[subset_index[c.glyphName]]], attributes(c)) for c in ours.components]
            if actual != expected:
                problems.append(f"{where}: components {actual}, expected {expected}")
        if outline(ours, subset_glyf) != outline(theirs, original_glyf):
            problems.append(f"{where}: the outline differs")
        if instructions(ours) != instructions(theirs):
            problems.append(f"{where}: the instructions differ")
        if subset_hmtx[ours_name] != original_hmtx[theirs_name]:
            problems.append(f"{where}: hmtx {subset_hmtx[ours_name]}, expected {original_hmtx[theirs_name]}")

    if problems:
        raise ValueError(f"{len(problems)} differences:\n" + "\n".join(problems[:20]))

    json.dump({"glyphs": len(ids)}, sys.stdout)


def main(argv):
    if len(argv) == 3 and argv[1] == "dump":
        command = lambda: dump(argv[2])
    elif len(argv) == 5 and argv[1] == "subset":
        command = lambda: check_subset(argv[2], argv[3], argv[4])
    else:
        print(__doc__, file=sys.stderr)
        return 2

    try:
        command()
    except Exception as error:  # any failure to read or match the font is this check failing
        print(f"{type(error).__name__}: {error}", file=sys.stderr)
        return 1

    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
