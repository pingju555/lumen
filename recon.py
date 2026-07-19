import os, zipfile

base = r"D:\Main\AI Quest&Project\桌面覆盖层"
kwgt_dir = os.path.join(base, "kwgt_aosp_release_382")
klwp_dir = os.path.join(base, "klwp_aosp_release_382")

def tree(d, label):
    print("=== %s assets tree ===" % label)
    for root, dirs, files in os.walk(os.path.join(d, "assets")):
        for f in files:
            p = os.path.join(root, f)
            print(" ", os.path.relpath(p, d), os.path.getsize(p))
    print()

tree(kwgt_dir, "KWGT")
tree(klwp_dir, "KLWP")

# inspect one widget preset internals
for name in ["Series.kwgt", "TextClock.kwgt"]:
    p = os.path.join(kwgt_dir, "assets", "widgets", name)
    print("=== %s internals ===" % name)
    z = zipfile.ZipFile(p)
    for n in z.namelist():
        print("  ", n, z.getinfo(n).file_size)
    print()
