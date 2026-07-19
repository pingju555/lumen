import zipfile, re, os, glob, collections

roots = [
    r"D:/Main/AI Quest&Project/桌面覆盖层/klwp_aosp_release_382/assets",
    r"D:/Main/AI Quest&Project/桌面覆盖层/kwgt_aosp_release_382/assets",
]
extras = [r"D:/Main/Coding/Tare CN WorkSpace/Klwe rekus/klwp_extracted/preset.json"]

func_re = re.compile(r'\$([a-zA-Z]{2,4})\(')
count = collections.Counter()
presets_with = collections.defaultdict(set)
examples = collections.defaultdict(list)
total_presets = 0
total_formulas = 0

def scan_text(text, label):
    global total_formulas
    for m in func_re.finditer(text):
        fn = m.group(1).lower()
        count[fn] += 1
        presets_with[fn].add(label)
        total_formulas += 1
        if len(examples[fn]) < 2:
            s = text[max(0, m.start() - 25):m.end() + 25].replace('\n', ' ')
            examples[fn].append(s.strip())

for root in roots:
    for path in glob.glob(os.path.join(root, '**', '*'), recursive=True):
        if path.lower().endswith(('.kwgt', '.komp', '.klwp')):
            total_presets += 1
            try:
                z = zipfile.ZipFile(path)
                for n in z.namelist():
                    if n.endswith('.json'):
                        try:
                            data = z.read(n).decode('utf-8', 'ignore')
                        except Exception:
                            continue
                        scan_text(data, os.path.basename(path))
            except Exception:
                pass

for ep in extras:
    if os.path.exists(ep):
        total_presets += 1
        try:
            data = open(ep, encoding='utf-8', errors='ignore').read()
            scan_text(data, os.path.basename(ep))
        except Exception:
            pass

print("TOTAL_PRESETS", total_presets)
print("TOTAL_FORMULA_CALLS", total_formulas)
print("DISTINCT_FUNCS", len(count))
print("--- function\tcalls\tpresets ---")
for fn, c in count.most_common():
    print(f"{fn}\t{c}\t{len(presets_with[fn])}")
print("--- examples ---")
for fn, _ in count.most_common():
    print(fn, "::", " | ".join(examples[fn]))
