import os, zipfile, json
from collections import Counter, defaultdict

base = r"D:\Main\AI Quest&Project\桌面覆盖层"

def load(path):
    try:
        z = zipfile.ZipFile(path)
    except Exception:
        return None, None
    for n in z.namelist():
        if os.path.basename(n) == "preset.json":
            try:
                return json.loads(z.read(n)), z.namelist()
            except Exception:
                return None, z.namelist()
    return None, z.namelist()

def collect(o, types, events, anim_count, has_img, has_btn):
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
                    events.append(e)
        an = o.get("internal_animations")
        if isinstance(an, list):
            anim_count[0] += len(an)
        for v in o.values():
            collect(v, types, events, anim_count, has_img, has_btn)
    elif isinstance(o, list):
        for v in o:
            collect(v, types, events, anim_count, has_img, has_btn)

# 1) widgets overview
print("=== WIDGETS (types / events / animations) ===")
wd = os.path.join(base, "kwgt_aosp_release_382", "assets", "widgets")
for f in sorted(os.listdir(wd)):
    p = os.path.join(wd, f)
    o, _ = load(p)
    if not o:
        print("  SKIP", f); continue
    types = set(); events = []; anim = [0]; img = [False]; btn = [False]
    collect(o, types, events, anim, img, btn)
    ev_counts = Counter(e.get("action") for e in events)
    print("  %-22s types=%s anim=%d events=%s img=%s btn=%s" % (
        f, sorted(types), anim[0], dict(ev_counts), img[0], btn[0]))

# 2) komp internal listing (first 3)
print("\n=== KOMP internal listing (sample) ===")
kc = os.path.join(base, "kwgt_aosp_release_382", "assets", "komponents")
for f in sorted(os.listdir(kc))[:4]:
    p = os.path.join(kc, f)
    try:
        z = zipfile.ZipFile(p)
        names = z.namelist()
    except Exception as e:
        print("  %s -> ERR %s" % (f, e)); continue
    print("  %s : %s" % (f, names[:8]))

# 3) klwp wallpapers overview
print("\n=== WALLPAPERS (klwp.zip) (types / events / animations) ===")
wl = os.path.join(base, "klwp_aosp_release_382", "assets", "wallpapers")
for f in sorted(os.listdir(wl)):
    p = os.path.join(wl, f)
    o, _ = load(p)
    if not o:
        print("  SKIP(no preset.json):", f); continue
    types = set(); events = []; anim = [0]; img = [False]; btn = [False]
    collect(o, types, events, anim, img, btn)
    ev_counts = Counter(e.get("action") for e in events)
    print("  %-22s types=%s anim=%d events=%s" % (f, sorted(types), anim[0], dict(ev_counts)))
