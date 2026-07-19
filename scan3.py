import os, zipfile, json
from collections import Counter

base = r"D:\Main\AI Quest&Project\桌面覆盖层"

def load_any(path):
    try:
        z = zipfile.ZipFile(path)
    except Exception:
        return None
    cands = [n for n in z.namelist() if os.path.basename(n) in ("preset.json", "komponent.json")]
    if not cands:
        return None
    # preset.json priority
    cands.sort(key=lambda n: 0 if os.path.basename(n) == "preset.json" else 1)
    try:
        return json.loads(z.read(cands[0]))
    except Exception:
        return None

def collect(o, types, events, anim_count, has_img, has_btn, anim_holders):
    if isinstance(o, dict):
        it = o.get("internal_type")
        if it:
            types.add(it)
            if "Image" in it: has_img[0] = True
            if "Button" in it: has_btn[0] = True
        ev = o.get("internal_events")
        if isinstance(ev, list):
            for e in ev:
                if isinstance(e, dict):
                    events.append(e.get("action"))
        an = o.get("internal_animations")
        if isinstance(an, list) and len(an) > 0:
            anim_count[0] += len(an)
            anim_holders.append((it, an))
        for v in o.values():
            collect(v, types, events, anim_count, has_img, has_btn, anim_holders)
    elif isinstance(o, list):
        for v in o:
            collect(v, types, events, anim_count, has_img, has_btn, anim_holders)

roots = [
    ("widgets", os.path.join(base, "kwgt_aosp_release_382", "assets", "widgets")),
    ("kwgt_komp", os.path.join(base, "kwgt_aosp_release_382", "assets", "komponents")),
    ("wallpapers", os.path.join(base, "klwp_aosp_release_382", "assets", "wallpapers")),
    ("klwp_komp", os.path.join(base, "klwp_aosp_release_382", "assets", "komponents")),
]
all_actions = Counter()
all_types = set()
anim_examples = []
for label, root in roots:
    for f in sorted(os.listdir(root)):
        p = os.path.join(root, f)
        o = load_any(p)
        if not o:
            print("SKIP", label, f); continue
        types = set(); events = []; anim = [0]; img = [False]; btn = [False]; holders = []
        collect(o, types, events, anim, img, btn, holders)
        all_actions.update(events)
        all_types.update(types)
        flag = ""
        if anim[0]: flag += " ANIM=%d" % anim[0]
        if img[0]: flag += " IMG"
        if btn[0]: flag += " BTN"
        if flag or events:
            print("%-10s %-26s types=%s ev=%s%s" % (label, f, sorted(types), dict(Counter(events)), flag))
        for it, an in holders:
            anim_examples.append((label, f, it, an))

print("\n=== ALL DISTINCT ACTIONS ===")
for a, c in all_actions.most_common():
    print("  ", a, c)
print("\n=== ALL DISTINCT TYPES ===")
for t in sorted(all_types):
    print("  ", t)
print("\n=== PRESETS WITH internal_animations (examples) ===")
for label, f, it, an in anim_examples:
    print("  ", label, f, it, "anim_count=", len(an))
    print("     sample:", json.dumps(an[0], ensure_ascii=False)[:400])
