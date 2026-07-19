import sys, zipfile, json, os

def load_preset(path):
    z = zipfile.ZipFile(path)
    target = None
    for n in z.namelist():
        base = os.path.basename(n)
        if base == "preset.json":
            target = n
            break
    if target is None:
        return None, z.namelist()
    data = z.read(target).decode("utf-8", "replace")
    return json.loads(data), z.namelist()

for path in sys.argv[1:]:
    print("\n\n########## %s ##########" % path)
    try:
        obj, names = load_preset(path)
        if obj is None:
            print("NO preset.json. entries:", names)
            continue
        print(json.dumps(obj, indent=1, ensure_ascii=False))
    except Exception as e:
        print("ERR", e)
