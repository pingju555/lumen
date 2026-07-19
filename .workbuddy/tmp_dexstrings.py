import sys, struct, glob, os

def read_uleb128(data, off):
    result = 0
    shift = 0
    while True:
        b = data[off]
        off += 1
        result |= (b & 0x7F) << shift
        if (b & 0x80) == 0:
            break
        shift += 7
    return result, off

def dump_dex(path):
    with open(path, 'rb') as f:
        data = f.read()
    if data[:4] != b'dex\n':
        return []
    string_ids_size = struct.unpack_from('<I', data, 0x38)[0]
    string_ids_off = struct.unpack_from('<I', data, 0x3C)[0]
    out = []
    for i in range(string_ids_size):
        s_off = struct.unpack_from('<I', data, string_ids_off + i * 4)[0]
        _, str_start = read_uleb128(data, s_off)
        end = str_start
        while data[end] != 0:
            end += 1
        raw = data[str_start:end]
        try:
            s = raw.decode('utf-8')
        except Exception:
            s = raw.decode('latin-1')
        out.append(s)
    return out

def main():
    d = sys.argv[1]
    kw = [k.lower() for k in sys.argv[2:]] if len(sys.argv) > 2 else None
    seen = set()
    for dex in sorted(glob.glob(os.path.join(d, 'classes*.dex'))):
        try:
            for s in dump_dex(dex):
                if s in seen:
                    continue
                if kw:
                    if not any(k in s.lower() for k in kw):
                        continue
                seen.add(s)
                print(s)
        except Exception as e:
            print(f"#ERR {os.path.basename(dex)}: {e}", file=sys.stderr)

if __name__ == '__main__':
    main()
