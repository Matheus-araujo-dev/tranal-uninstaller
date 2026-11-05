"""Extrai o recurso RT_MANIFEST (id 24) de um PE e imprime o XML.

Uso: python dump_manifest.py <exe> [--list]
"""
import struct
import sys

RT_MANIFEST = 24


def u16(b, o):
    return struct.unpack_from("<H", b, o)[0]


def u32(b, o):
    return struct.unpack_from("<I", b, o)[0]


def rva_to_offset(b, rva, sections):
    for va, vsz, rawp, rawsz in sections:
        if va <= rva < va + max(vsz, rawsz):
            return rawp + (rva - va)
    return None


def parse_dir(b, res_off, off, level, path, sections, out):
    """Percorre diretorios de recurso. path = (type, name, lang)."""
    if off is None:
        return
    n_named = u16(b, off + 12)
    n_id = u16(b, off + 14)
    base = off + 16
    for i in range(n_named + n_id):
        entry = base + i * 8
        name_or_id = u32(b, entry)
        target = u32(b, entry + 4)

        is_name = bool(name_or_id & 0x80000000)
        if is_name:
            noff = res_off + (name_or_id & 0x7FFFFFFF)
            nlen = u16(b, noff)
            ident = b[noff + 2:noff + 2 + nlen * 2].decode("utf-16-le")
        else:
            ident = name_or_id

        new_path = path + (ident,)

        if target & 0x80000000:
            parse_dir(b, res_off, res_off + (target & 0x7FFFFFFF), level + 1,
                      new_path, sections, out)
        else:
            data_entry = res_off + target
            rva = u32(b, data_entry)
            size = u32(b, data_entry + 4)
            fo = rva_to_offset(b, rva, sections)
            if fo is not None:
                out.append((new_path, b[fo:fo + size]))


def main():
    path = sys.argv[1]
    listing = "--list" in sys.argv
    b = open(path, "rb").read()
    pe = u32(b, 0x3C)
    if b[pe:pe + 4] != b"PE\0\0":
        raise SystemExit("nao e um PE")
    nsec = u16(b, pe + 6)
    opt_size = u16(b, pe + 20)
    opt = pe + 24
    magic = u16(b, opt)
    dd = opt + (112 if magic == 0x20B else 96)
    res_rva = u32(b, dd + 2 * 8)

    sections = []
    so = opt + opt_size
    for i in range(nsec):
        e = so + i * 40
        vsz, va, rawsz, rawp = u32(b, e + 8), u32(b, e + 12), u32(b, e + 16), u32(b, e + 20)
        sections.append((va, vsz, rawp, rawsz))

    res_off = rva_to_offset(b, res_rva, sections)
    out = []
    parse_dir(b, res_off, res_off, 0, (), sections, out)

    print(f"PE: {path}")
    print(f"resource dir: RVA=0x{res_rva:X} offset=0x{res_off:X}  entradas={len(out)}")
    for ids, data in out:
        t = ids[0] if ids else "?"
        if t == RT_MANIFEST:
            xml = data.decode("utf-8", "replace").rstrip("\x00")
            print(f"\n=== RT_MANIFEST ids={ids} ({len(data)} bytes) ===")
            print(xml)
        elif listing:
            print(f"recurso tipo={t} ids={ids} ({len(data)} bytes)")


if __name__ == "__main__":
    main()
