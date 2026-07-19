import sys, os, zipfile, json

base = r"D:\Main\AI Quest&Project\桌面覆盖层"

def load_any(path):
    z = zipfile.ZipFile(path)
    cands = [n for n in z.namelist() if os.path.basename(n) in ("preset.json", "komponent.json")]
    cands.sort(key=lambda n: 0 if os.path.basename(n) == "preset.json" else 1)
    return json.loads(z.read(cands[0]))

def safe(v, depth=0):
    if isinstance(v, str) and len(v) > 200:
        return "<%d chars>" % len(v)
    if isinstance(v, (dict, list)) and depth < 3:
        if isinstance(v, dict):
            return {k: safe(val, depth + 1) for k, val in v.items()}
        return [safe(x, depth + 1) for x in v]
    if isinstance(v, list) and len(v) > 6:
        return [safe(x, depth + 1) for x in v[:6]] + ["...(+%d)" % (len(v) - 6)]
    return v

def walk(o, out, ctx_type=None):
    if isinstance(o, dict):
        it = o.get("internal_type")
        if "internal_events" in o and isinstance(o["internal_events"], list):
            for e in o["internal_events"]:
                out["events"].append({"_owner": it, **safe(e)})
        if it == "BitmapModule":
            out["bitmaps"].append({"_owner": ctx_type, **safe(o)})
        if "internal_animations" in o and isinstance(o["internal_animations"], list) and o["internal_animations"]:
            out["anims"].append({"_owner": it, "items": safe(o["internal_animations"])})
        vt = o.get("viewgroup_items")
        if isinstance(vt, list):
            for c in vt:
                walk(c, out, it)
        for k, v in o.items():
            if k in ("viewgroup_items", "internal_events", "internal_animations"):
                continue
            if isinstance(v, (dict, list)):
                walk(v, out, it)
    elif isinstance(o, list):
        for v in o:
            walk(v, out, ctx_type)

mode = sys.argv[1]
if mode == "detail":
    p = sys.argv[2]
    o = load_any(p)
    out = {"events": [], "bitmaps": [], "anims": []}
    # globals
    g = o.get("preset_root", {}).get("globals_list") or o.get("globals_list")
    walk(o, out)
    print("FILE:", p)
    print("GLOBALS_LIST:", json.dumps(safe(g), ensure_ascii=False, indent=1))
    print("EVENTS(%d):" % len(out["events"]))
    for e in out["events"]:
        print("  ", json.dumps(e, ensure_ascii=False))
    print("BITMAPS(%d):" % len(out["bitmaps"]))
    for b in out["bitmaps"]:
        print("  ", json.dumps(b, ensure_ascii=False))
    print("ANIMS(%d):" % len(out["anims"]))
    for a in out["anims"]:
        print("  ", json.dumps(a, ensure_ascii=False))

elif mode == "find":
    root = sys.argv[2]
    tname = sys.argv[3]
    for f in sorted(os.listdir(root)):
        p = os.path.join(root, f)
        try:
            o = load_any(p)
        except Exception:
            continue
        types = set()
        def gather(x):
            if isinstance(x, dict):
                if x.get("internal_type") == tname:
                    types.add(True)
                for v in x.values():
                    gather(v)
            elif isinstance(x, list):
                for v in x:
                    gather(v)
        gather(o)
        if types:
            print("FOUND", tname, "in", f)
            # dump first instance fields
            out = {"bitmaps": []}
            walk(o, out)
            for b in out["bitmaps"][:1]:
                print("  ", json.dumps(b, ensure_ascii=False)[:1500])
            break
