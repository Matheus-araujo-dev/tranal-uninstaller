"""Compara o resultado de remocao do Tranal com o do Revo Uninstaller Pro.

Uso: python compare_removal.py inventario-instalado.json inventario-depois-tranal.json inventario-depois-revo-nativo.json
"""
import json
import sys

INSTALADO, TRANAL, REVO = sys.argv[1], sys.argv[2], sys.argv[3]

inst = json.load(open(INSTALADO, encoding="utf-8"))
tran = json.load(open(TRANAL, encoding="utf-8"))
revo = json.load(open(REVO, encoding="utf-8"))

HKLM_KEY = "HKLM\\SOFTWARE\\VS Revo Group"
HKCU_KEY = "HKCU\\SOFTWARE\\VS Revo Group"
PD = "C:\\ProgramData\\VS Revo Group"
LA = "C:\\Users\\Matheus\\AppData\\Local\\VS Revo Group"


def yesno(v):
    return "SIM" if v else "nao"


def reg(d, key):
    return d["registry"].get(key, {"exists": False, "entries": []})


def rows():
    yield ("Pasta de instalacao (existe)",
           yesno(inst["install_dir"]["exists"]),
           yesno(tran["install_dir"]["exists"]),
           yesno(revo["install_dir"]["exists"]))
    yield ("Arquivos na pasta de instalacao",
           str(inst["install_dir"]["file_count"]),
           str(tran["install_dir"]["file_count"]),
           str(revo["install_dir"]["file_count"]))
    yield ("Chave de desinstalacao (HKLM)",
           yesno(inst["uninstall_key"]["HKLM"]),
           yesno(tran["uninstall_key"]["HKLM"]),
           yesno(revo["uninstall_key"]["HKLM"]))
    yield ("App Paths 64 bits (RevoUninPro.exe)",
           yesno(inst["app_paths"]["SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\App Paths\\RevoUninPro.exe"]),
           yesno(tran["app_paths"]["SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\App Paths\\RevoUninPro.exe"]),
           yesno(revo["app_paths"]["SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\App Paths\\RevoUninPro.exe"]))
    yield ("App Paths 32 bits (RevoUninPro.exe)",
           yesno(inst["app_paths"]["SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\App Paths\\RevoUninPro.exe"]),
           yesno(tran["app_paths"]["SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\App Paths\\RevoUninPro.exe"]),
           yesno(revo["app_paths"]["SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\App Paths\\RevoUninPro.exe"]))
    yield ("Servico/driver Revoflt (HKLM)",
           yesno(inst["service"]["HKLM"]),
           yesno(tran["service"]["HKLM"]),
           yesno(revo["service"]["HKLM"]))
    yield ("HKLM\\SOFTWARE\\VS Revo Group",
           yesno(reg(inst, HKLM_KEY)["exists"]),
           yesno(reg(tran, HKLM_KEY)["exists"]),
           yesno(reg(revo, HKLM_KEY)["exists"]))
    yield ("HKCU\\SOFTWARE\\VS Revo Group (entradas)",
           str(len(reg(inst, HKCU_KEY)["entries"])),
           str(len(reg(tran, HKCU_KEY)["entries"])),
           str(len(reg(revo, HKCU_KEY)["entries"])))
    yield ("ProgramData\\VS Revo Group (arquivos)",
           str(inst["data_dirs"][PD]["file_count"]),
           str(tran["data_dirs"][PD]["file_count"]),
           str(revo["data_dirs"][PD]["file_count"]))
    yield ("LocalAppData\\VS Revo Group (arquivos)",
           str(inst["data_dirs"][LA]["file_count"]),
           str(tran["data_dirs"][LA]["file_count"]),
           str(revo["data_dirs"][LA]["file_count"]))


def main():
    print("Remocao do Revo Uninstaller Pro 5.5.4 — comparacao Tranal x Revo")
    print()
    print(f"{'ARTEFATO':46} | {'INSTALADO':>14} | {'TRANAL':>10} | {'REVO':>10}")
    print("-" * 90)
    for a, b, c, d in rows():
        print(f"{a:46} | {b:>14} | {c:>10} | {d:>10}")

    print()
    leftovers_revo = set(revo["data_dirs"][LA]["files"]) | set(revo["data_dirs"][PD]["files"])
    leftovers_tran = set(tran["data_dirs"][LA]["files"]) | set(tran["data_dirs"][PD]["files"])
    extra = sorted(leftovers_revo - leftovers_tran)
    print(f"Sobras de dados apos a remocao: Tranal={len(leftovers_tran)}  Revo={len(leftovers_revo)}")
    if extra:
        print("Itens que o Revo deixou e o Tranal removeu:")
        for f in extra:
            print(f"  - {f}")


if __name__ == "__main__":
    main()
