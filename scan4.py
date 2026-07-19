import os, zipfile, json
from collections import defaultdict

base = r"D:\Main\AI Quest&Project\桌面覆盖层"

def load_any(path):
    z = zipfile.ZipFile(path)
    cands = [n for n in z.namelist() if os.path.basename(n) in ("preset.json", "komponent.json")]
    cands.sort(key=lambda n: 0 if os.path.basename(n) == "preset.json" else 1)
    return json.loads(z.read(cands[0]))

SKIP = {"internal_type", "viewgroup_items", "internal_events", "internal_animations",
        "internal_globals", "internal_formulas", "internal_toggles"}

type_fields = defaultdict(set)
bgfx = defaultdict(list)

def walk(o):
    if isinstance(o, dict):
        it = o.get("internal_type")
        if it:
            for k in o:
                if k in SKIP:
                    continue
                type_fields[it].add(k)
        for k, v in o.items():
            if (k.startswith("background_") or k.startswith("fx_")) and k not in bgfx:
                if isinstance(v, (dict, list)):
                    bgfx[k].append("<obj>")
                else:
                    s = str(v)
                    if len(s) > 120:
                        s = "<%d chars>" % len(s)
                    if len(bgfx[k]) < 4:
                        bgfx[k].append(s)
            if isinstance(v, (dict, list)):
                walk(v)
    elif isinstance(o, list):
        for v in o:
            walk(v)

roots = [
    os.path.join(base, "kwgt_aosp_release_382", "assets", "widgets"),
    os.path.join(base, "kwgt_aosp_release_382", "assets", "komponents"),
    os.path.join(base, "klwp_aosp_release_382", "assets", "wallpapers"),
    os.path.join(base, "klwp_aosp_release_382", "assets", "komponents"),
]
for root in roots:
    for f in sorted(os.listdir(root)):
        p = os.path.join(root, f)
        try:
            o = load_any(p)
        except Exception:
            continue
        walk(o)

print("=== REAL FIELD NAMES PER ATOM/Layer TYPE ===")
for t in sorted(type_fields):
    print("\n[%s]" % t)
    for k in sorted(type_fields[t]):
        print("   ", k)

print("\n=== BACKGROUND / FX FIELDS (samples) ===")
for k in sorted(bgfx):
    print("  ", k, "=>", bgfx[k][:4])
