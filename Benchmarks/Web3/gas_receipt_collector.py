#!/usr/bin/env python3
"""Colleziona gasUsed delle 3 transazioni Request/VRF/Claim e confronta
le classi di numerosità a PARITÀ di percorso di storage.

Il numero di corpi NON è deducibile dal contratto; un seed_body_counts.csv
calcolato con l'algoritmo Unity è necessario per stimare GAS_*_DELTA.
"""
from __future__ import annotations
import argparse
import os
import statistics
import sys
from decimal import Decimal
from pathlib import Path
from sys_web3_common import ROOT, GAS_COLS, get_rpc_context, load_json, load_obs, save_csv


def parse():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--state", type=Path, default=ROOT / "output/web3_state.json")
    p.add_argument("--out", type=Path, default=ROOT / "output")
    p.add_argument("--rpc", default=os.environ.get("SYS_RPC_URL", "https://ethereum-sepolia-rpc.publicnode.com"))
    p.add_argument("--abi", type=Path, default=ROOT / "resources/smart_contract_abi.json")
    p.add_argument("--address-file", type=Path, default=ROOT / "resources/smart_contract_address.txt")
    p.add_argument("--body-counts", type=Path, default=ROOT / "output/seed_body_counts.csv")
    p.add_argument("--min-per-group", type=int, default=3)
    p.add_argument("--offline-receipts", type=Path, help="JSON locale tx_hash -> {status,gasUsed,effectiveGasPrice}; SOLO TEST senza rete")
    return p.parse_args()


def main():
    a = parse()
    state = load_json(a.state)
    if not state or not isinstance(state.get("cycles"), list):
        raise ValueError(f"Stato non valido: {a.state}")
    mapping = {}
    if a.body_counts.is_file():
        for row in load_obs(a.body_counts, "seed").values():
            if not row.get("body_count", "").isdigit():
                raise ValueError("body_count deve essere intero positivo")
            b = int(row["body_count"])
            if b < 1:
                raise ValueError("body_count deve essere >= 1")
            version = row.get("algorithm_version") or row.get("version")
            if not version:
                raise ValueError("seed_body_counts.csv richiede algorithm_version per garantire coerenza")
            mapping[(int(row["seed"]), int(version))] = b
    else:
        print("[AVVISO] seed_body_counts.csv non trovato: body_count vuoto, GAS_*_DELTA non stimabile.")
    if a.offline_receipts:
        fixtures = load_json(a.offline_receipts)
        if not isinstance(fixtures, dict):
            raise ValueError("--offline-receipts deve essere un oggetto JSON indicizzato per tx hash")
        def receipt_for(h):
            r = fixtures.get(h.lower()) or fixtures.get(h)
            if r is None:
                raise LookupError(h)
            return r
    else:
        w3, contract = get_rpc_context(a.rpc, a.abi, a.address_file)
        if contract.address.lower() != state["contract"].lower():
            raise RuntimeError("Indirizzo dello stato diverso dal contratto RPC")
        if int(state["chain_id"]) != 11155111:
            raise RuntimeError("Lo stato non è Sepolia")
        def receipt_for(h):
            return w3.eth.get_transaction_receipt(h)
    rows, errors = [], []
    for c in state["cycles"]:
        request_id = str(c.get("request_id", "") or f"attempt:{c['attempt']}")
        num = ""
        if c.get("seed") is not None and c.get("version") is not None:
            num = mapping.get((int(c["seed"]), int(c["version"])), "")
        for operation, key, case in (
            ("request", "request_tx_hash", c.get("request_storage_case", "request:unclassified")),
            ("vrf", "vrf_tx_hash", "vrf:fulfilled"),
            ("claim", "claim_tx_hash", c.get("claim_storage_case", "claim:unclassified"))):
            h = c.get(key)
            if not h:
                continue
            try:
                receipt = receipt_for(h)
                rows.append(dict(request_id=request_id, operation=operation, tx_hash=h,
                        status=int(receipt["status"]), gas_used=int(receipt["gasUsed"]),
                        effective_gas_price_wei=int(receipt.get("effectiveGasPrice", 0)),
                        body_count=num, storage_case=case))
            except Exception as exc:
                errors.append(f"{request_id}, {operation}, {h}: {type(exc).__name__}: {exc}")
    save_csv(a.out / "gas_receipts.csv", GAS_COLS, rows)
    stats = []
    for op in ("request", "vrf", "claim"):
        subset = [r for r in rows if r["operation"] == op]
        good = [r["gas_used"] for r in subset if r["status"] == 1]
        stats.append(dict(operation=op, receipts=len(subset), successful=len(good),
                  mean_gas=(round(statistics.mean(good), 3) if good else ""),
                  std_gas=(round(statistics.stdev(good), 3) if len(good) >= 2 else ""),
                  min_gas=min(good) if good else "", max_gas=max(good) if good else ""))
    save_csv(a.out / "gas_stats.csv", ["operation", "receipts", "successful", "mean_gas", "std_gas", "min_gas", "max_gas"], stats)
    deltas = []
    for op in ("request", "vrf", "claim"):
        cases = sorted(set(str(r["storage_case"]) for r in rows if r["operation"] == op))
        for case in cases:
            entries = [r for r in rows if r["operation"] == op and r["storage_case"] == case
                       and r["status"] == 1 and r["body_count"] != ""]
            entries.sort(key=lambda r: (int(r["body_count"]), r["tx_hash"]))
            if len(entries) < 2 * a.min_per_group or len({r["body_count"] for r in entries}) < 2:
                continue
            # Cerca una soglia sul conteggio intero che preservi tutti i
            # campioni con lo stesso body_count nello stesso gruppo.
            values = sorted({int(r["body_count"]) for r in entries})
            choices = []
            for threshold in values[:-1]:
                low_candidate = [r for r in entries if int(r["body_count"]) <= threshold]
                high_candidate = [r for r in entries if int(r["body_count"]) > threshold]
                if min(len(low_candidate), len(high_candidate)) >= a.min_per_group:
                    choices.append((abs(len(low_candidate)-len(high_candidate)), threshold,
                                    low_candidate, high_candidate))
            if not choices:
                continue
            _, _, low, high = min(choices, key=lambda t: (t[0], t[1]))
            low_mean = statistics.mean(r["gas_used"] for r in low)
            high_mean = statistics.mean(r["gas_used"] for r in high)
            deltas.append(dict(operation=op, storage_case=case, low_samples=len(low),
                    high_samples=len(high), low_max_bodies=max(int(r["body_count"]) for r in low),
                    high_min_bodies=min(int(r["body_count"]) for r in high),
                    low_mean_gas=round(low_mean, 3), high_mean_gas=round(high_mean, 3),
                    abs_delta_gas=round(abs(high_mean - low_mean), 3)))
    delta_cols = ["operation", "storage_case", "low_samples", "high_samples", "low_max_bodies", "high_min_bodies", "low_mean_gas", "high_mean_gas", "abs_delta_gas"]
    save_csv(a.out / "gas_group_summary.csv", delta_cols, deltas)
    print("Gas receipt raccolte:", len(rows), "| errori RPC:", len(errors))
    for s in stats:
        print(f"  {s['operation']:7s}: {s['successful']}/{s['receipts']} successi, media {s['mean_gas']} gas")
    for op in ("request", "vrf", "claim"):
        options = sorted((d for d in deltas if d["operation"] == op),
                         key=lambda d: (-(d["low_samples"] + d["high_samples"]), d["storage_case"]))
        if options:
            d = options[0]
            print(f"  DELTA {op}: {d['abs_delta_gas']} gas | caso {d['storage_case']} "
                  f"(gruppi {d['low_samples']}/{d['high_samples']})")
        else:
            print(f"  DELTA {op}: NON DETERMINABILE (seed/body_count o gruppi omogenei insufficienti)")
    if errors:
        (a.out / "gas_receipt_errors.txt").write_text("\n".join(errors) + "\n", encoding="utf-8")
        print("Rivedere gas_receipt_errors.txt; non utilizzare statistiche parziali senza dichiararlo.", file=sys.stderr)
        return 2
    return 0

if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as exc:
        print(f"ERRORE: {type(exc).__name__}: {exc}", file=sys.stderr)
        sys.exit(1)
