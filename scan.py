import os, zipfile, json
from collections import Counter

def load(path):
    try:
        z = zipfile.ZipFile(path)
    except Exception:
        return None
    for n in z.namelist():
        if os.path.basename(n) == "preset.json":
            try:
                return json.loads(z.read(n))
            except Exception:
                return None
    return None

def walk(o, types, events, globals_, formulas_keys, globals_keys, anim_fields):
    if isinstance(o, dict):
        it = o.get("internal_type")
        if it:
            types.add(it)
        ev = o.get("internal_events")
        if ev:
            for e in ev:
                if isinstance(e, dict):
                    events.add(e.get("action") or str(e))
        gl = o.get("globals_list")
        if isinstance(gl, dict):
            for k, v in gl.items():
                globals_[v.get("type")] += 1
        inf = o.get("internal_formulas")
        if isinstance(inf, dict):
            for k in inf:
                formulas_keys.add(k)
        ing = o.get("internal_globals")
        if isinstance(ing, dict):
            for k in ing:
                globals_keys.add(k)
        for k in o:
            kl = k.lower()
            if "anim" in kl or "react" in kl or k in ("trigger", "triggers", "action", "actions"):
                anim_fields.add(k)
        for k, v in o.items():
            walk(v, types, events, globals_, formulas_keys, globals_keys, anim_fields)
    elif isinstance(o, list):
        for v in o:
            walk(v, types, events, globals_, formulas_keys, globals_keys, anim_fields)

base = r"D:\Main\AI Quest&Project\桌面覆盖层"
types = set(); events = set(); globals_ = Counter()
formulas_keys = set(); globals_keys = set(); anim_fields = set()

roots = [
    ("widgets", os.path.join(base, "kwgt_aosp_release_382", "assets", "widgets")),
    ("kwgt_komponents", os.path.join(base, "kwgt_aosp_release_382", "assets", "komponents")),
    ("wallpapers", os.path.join(base, "klwp_aosp_release_382", "assets", "wallpapers")),
    ("klwp_komponents", os.path.join(base, "klwp_aosp_release_382", "assets", "komponents")),
]
for label, root in roots:
    for f in sorted(os.listdir(root)):
        p = os.path.join(root, f)
        o = load(p)
        if o is None:
            print("SKIP(no preset.json):", label, f)
            continue
        walk(o, types, events, globals_, formulas_keys, globals_keys, anim_fields)

print("=== INTERNAL_TYPES (atom/layer kinds) ===")
for t in sorted(types):
    print("  ", t)
print("\n=== EVENTS / ACTIONS seen ===")
for e in sorted(events):
    print("  ", e)
print("\n=== ANIM/REACT/TRIGGER FIELD NAMES ===")
for a in sorted(anim_fields):
    print("  ", a)
print("\n=== GLOBAL VARIABLE TYPES ===")
print("  ", dict(globals_))
print("\n=== PROPS BOUND TO FORMULAS (internal_formulas keys) ===")
for k in sorted(formulas_keys):
    print("  ", k)
print("\n=== PROPS BOUND TO GLOBALS (internal_globals keys) ===")
for k in sorted(globals_keys):
    print("  ", k)
