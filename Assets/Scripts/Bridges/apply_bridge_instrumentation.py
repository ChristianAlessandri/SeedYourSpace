#!/usr/bin/env python3
"""Applica la strumentazione Unity preservando gli EOL (anche CRLF) e creando backup.
Esempio: python apply_bridge_instrumentation.py --project C:/Percorso/SYS --dry-run
"""
import argparse
import shutil
from pathlib import Path


def patch_one(path, edits, dry_run):
    raw = path.read_bytes()
    eol = '\r\n' if b'\r\n' in raw else '\n'
    original = raw.decode('utf-8-sig')
    norm = original.replace('\r\n','\n')
    new = norm
    for find, replacement in edits:
        if replacement in new:
            continue
        if new.count(find) != 1:
            raise RuntimeError(f"Match non univoco in {path}: {find!r}")
        new = new.replace(find,replacement,1)
    if new == norm:
        print('Già strumentato:', path)
        return
    print('Modifica:',path, '| backup:',path.with_suffix(path.suffix+'.sysb_backup'))
    if not dry_run:
        backup = path.with_suffix(path.suffix + '.sysb_backup')
        if backup.exists():
            raise RuntimeError(f"Backup esistente {backup}; revisionare manualmente prima di sovrascrivere")
        shutil.copy2(path,backup)
        path.write_bytes(new.replace('\n',eol).encode('utf-8-sig' if raw.startswith(b'\xef\xbb\xbf') else 'utf-8'))


def main():
    pa=argparse.ArgumentParser(description=__doc__)
    pa.add_argument('--project', type=Path, required=True)
    pa.add_argument('--dry-run', action='store_true')
    a=pa.parse_args()
    web=a.project/'Assets/Scripts/Web3/InventoryUICard.cs'
    gen=a.project/'Assets/Scripts/Star System/Logic/StarSystemGenerator.cs'
    if not web.exists() or not gen.exists():
        raise RuntimeError('Non trovati InventoryUICard.cs e StarSystemGenerator.cs nei percorsi originali')
    patch_one(web,[
        ('    public BigInteger algorithmVersion;',
         '    public BigInteger algorithmVersion;\n    private BigInteger systemTokenId;'),
        ('        systemSeed = seed;',
         '        systemTokenId = tokenId;\n        systemSeed = seed;'),
        ('            SystemDataBridge.TargetSeed = systemSeed.ToString();',
         '            Web3BridgeBenchmarkLog.Staged(systemTokenId, systemSeed, algorithmVersion);\n            SystemDataBridge.TargetSeed = systemSeed.ToString();')
    ],a.dry_run)
    patch_one(gen,[
        ('            algorithmVersion = SystemDataBridge.TargetVersion;',
         '            algorithmVersion = SystemDataBridge.TargetVersion;\n            Web3BridgeBenchmarkLog.OnConsumed(masterSeed, algorithmVersion);')
    ],a.dry_run)
    helper=Path(__file__).with_name('Web3BridgeBenchmarkLog.cs')
    target=a.project/'Assets/Scripts/Benchmarks/Web3BridgeBenchmarkLog.cs'
    print('Helper:',helper,'->',target)
    if not a.dry_run:
        target.parent.mkdir(parents=True,exist_ok=True)
        if target.exists():
            if target.read_bytes()!=helper.read_bytes():
                raise RuntimeError('Helper già esistente ma diverso; revisionarlo prima di sovrascrivere')
        else:
            shutil.copy2(helper,target)
    print('Operazione completata' if not a.dry_run else 'Dry run riuscito; nessun file modificato')

if __name__ == '__main__':
    main()
