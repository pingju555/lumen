import os, zipfile, json
from collections import defaultdict

base = r"D:\Main\AI Quest&Project\桌面覆盖层"

def load_any(path):
    z = zipfile.ZipFile(path)
    cands = [n for n in z.namelist() if os.path.basename(n) in ("preset.json", "komponent.json")]
    cands.sort(key=lambda n: 0 if os.path.basename(n) == "preset.json" else 1)
    return json.loads(z.read(cands[0]))

ENUMS = [
    "shape_type", "shape_angle", "shape_corners",
    "fx_gradient", "fx_shadow", "fx_mask", "fx_shadow_direction",
    "series_series", "style_style", "style_mode", "style_gmode", "style_align",
    "config_stacking", "config_fx", "config_tiling", "config_fx_fcolor",
    "background_type", "color_mode", "progress_mode", "paint_mode", "paint_style",
    "text_filter", "text_size_type", "bitmap_mode", "icon_set", "icon_rotate_mode",
    "series_rotate_mode", "shape_rotate_mode", "text_rotate_mode", "config_rotate_mode",
]
vals = defaultdict(set)

def walk(o):
    if isinstance(o, dict):
        for k, v in o.items():
            if k in ENUMS and isinstance(v, (str, int, float, bool)):
                vals[k].add(v)
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

print("=== ENUM FIELD VALUES (from real presets) ===")
for k in ENUMS:
    if vals[k]:
        print("  %-22s => %s" % (k, sorted(vals[k], key=str)))

# config.json top-level keys + look for action/anim/shape schemas
print("\n=== assets/config.json probe ===")
cfg = os.path.join(base, "kwgt_aosp_release_382", "assets", "config.json")
with open(cfg, encoding="utf-8", errors="replace") as fh:
    cj = json.load(fh)
print("  top keys:", list(cj.keys())[:40])
for interesting in ("actions", "animations", "animation", "triggers", "shape", "shapes", "modules", "module_types", "globals"):
    if interesting in cj:
        v = cj[interesting]
        print("  FOUND config.json['%s'] (type %s):" % (interesting, type(v).__name__))
        if isinstance(v, (list, dict)):
            print("    ", json.dumps(v, ensure_ascii=False)[:600])
        else:
            print("    ", str(v)[:300])
