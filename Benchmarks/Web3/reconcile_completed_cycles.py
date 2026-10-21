#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Riconcilia cicli SYS con claim già MINATO ma stato locale != complete.

Default: diagnostica sola lettura (RPC GET/eth_call, nessuna firma/broadcast).
--apply: esegue il riallineamento locale SOLTANTO se TUTTI i cicli incompleti
superano le verifiche on-chain; esegue un backup della fonte JSON.

Installazione: copiare questo file accanto a web3_cycle_audit.py e
sys_web3_common.py; utilizzare lo stesso ambiente Python del Blocco B.

    python reconcile_completed_cycles.py --out output
    python reconcile_completed_cycles.py --out output --apply

Non richiede né carica SYS_PRIVATE_KEY. Non scrive dati numerici inventati:
le latenze non osservate originariamente restano vuote nei CSV.
"""
from __future__ import annotations

import argparse
import csv
import json
import os
import shutil
import sys
from datetime import datetime, timezone
from pathlib import Path

from sys_web3_common import (
    ROOT, get_rpc_context, event_in_receipt, load_json,
    load_obs, now, save_json,
)
from web3_cycle_audit import persist

DEFAULT_RPC = "https://ethereum-sepolia-rpc.publicnode.com"


def cli():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--out", type=Path, default=Path("output"))
    p.add_argument("--rpc", default=os.environ.get("SYS_RPC_URL", DEFAULT_RPC))
    p.add_argument("--abi", type=Path, default=ROOT / "resources/smart_contract_abi.json")
    p.add_argument("--address-file", type=Path, default=ROOT / "resources/smart_contract_address.txt")
    p.add_argument("--cycles", type=int, default=30)
    p.add_argument("--unity-observations", type=Path, default=None, help="CSV Unity reale con token_id,seed,algorithm_version; preserva evidenza bridge")
    p.add_argument("--apply", action="store_true", help="salva stato e CSV dopo verifica on-chain completa")
    return p.parse_args()


def inbool(value):
    if isinstance(value, bool):
        return value
    return str(value).strip().lower() in {"true", "1", "yes"}


def no_timing_stage(c: dict, phase: str, tx_hash: str, block: int) -> None:
    """Risultato accertato on-chain; nessuna durata retrodatata/inventata."""
    stages = c.setdefault("stages", {})
    previous = stages.get(phase, {})
    if inbool(previous.get("completed", False)):
        return
    stages[phase] = {
        "start_time": "", "end_time": "", "elapsed_s": "",
        "completed": True, "tx_hash": tx_hash, "block_number": block,
    }


def inspect_cycle(w3, contract, wallet: str, c: dict) -> dict:
    attempt = c.get("attempt", "?")
    req_id = c.get("request_id")
    claim_hash = c.get("claim_tx_hash")
    if not req_id or not claim_hash:
        raise ValueError(f"ciclo {attempt}: request_id o claim_tx_hash assente; nessuna riconciliazione automatica")
    if c.get("seed") is None or c.get("version") is None:
        raise ValueError(f"ciclo {attempt}: seed/versione assenti dallo stato locale")
    if not inbool(c.get("request_receipt_ok", False)) or not c.get("vrf_tx_hash"):
        raise ValueError(f"ciclo {attempt}: richiesta o callback VRF non documentata")
    receipt = w3.eth.get_transaction_receipt(claim_hash)
    if int(receipt["status"]) != 1:
        raise ValueError(f"ciclo {attempt}: la receipt claim ha status != 1")
    if str(receipt.get("to", "")).lower() != contract.address.lower():
        raise ValueError(f"ciclo {attempt}: claim diretto ad altro contratto")
    event = event_in_receipt(contract, "SystemClaimed", receipt, requestId=int(req_id))
    tid = int(event["tokenId"])
    seed_event = int(event["seed"])
    if str(event["owner"]).lower() != wallet.lower():
        raise ValueError(f"ciclo {attempt}: owner evento claim inatteso")
    seed, version = contract.functions.systems(tid).call()
    owner = contract.functions.ownerOf(tid).call()
    if str(owner).lower() != wallet.lower():
        raise ValueError(f"ciclo {attempt}: ownerOf({tid}) diverso dal wallet")
    if int(seed) != int(c["seed"]) or int(seed_event) != int(c["seed"]):
        raise ValueError(f"ciclo {attempt}: seed in eventi/storage discordante")
    if int(version) != int(c["version"]):
        raise ValueError(f"ciclo {attempt}: versione storage discordante")
    if c.get("request_algorithm_version") is not None and int(version) != int(c["request_algorithm_version"]):
        raise ValueError(f"ciclo {attempt}: versione diversa da SystemRequested")
    if c.get("token_id") not in (None, "") and int(c["token_id"]) != tid:
        raise ValueError(f"ciclo {attempt}: token ID discordante")
    balance = int(contract.functions.balanceOf(wallet).call())
    if not any(int(contract.functions.tokenOfOwnerByIndex(wallet, j).call()) == tid for j in range(balance)):
        raise ValueError(f"ciclo {attempt}: token {tid} non presente in ERC721Enumerable")
    if not contract.functions.previewSVG(int(seed)).call():
        raise ValueError(f"ciclo {attempt}: previewSVG restituisce un output vuoto")
    return {
        "attempt": int(attempt), "token_id": tid, "seed": str(int(seed)),
        "version": str(int(version)), "tx_hash": claim_hash,
        "block": int(receipt["blockNumber"]),
        "fee_wei": int(receipt["gasUsed"]) * int(receipt.get("effectiveGasPrice", 0)),
    }


def main() -> int:
    args = cli()
    state_path = args.out / "web3_state.json"
    state = load_json(state_path)
    if not isinstance(state, dict) or not isinstance(state.get("cycles"), list):
        raise ValueError(f"Stato JSON mancante o malformato: {state_path}")
    cycles = state["cycles"]
    if len(cycles) != args.cycles:
        raise ValueError(f"Numero di cicli JSON {len(cycles)} != attesi {args.cycles}")
    incomplete = [c for c in cycles if c.get("status") != "complete"]
    print(f"Cicli JSON: {len(cycles)} | complete: {len(cycles) - len(incomplete)} | non completi: {len(incomplete)}")
    for c in incomplete:
        print(
            f"  tentativo {c.get('attempt')}: status={c.get('status')}; "
            f"claim_receipt_ok={c.get('claim_receipt_ok')}; token={c.get('token_id')}; "
            f"import.completed={c.get('stages',{}).get('import',{}).get('completed')}; "
            f"errore={c.get('last_error', '')}"
        )
    detail_path = args.out / "web3_details.csv"
    existing_details = []
    if detail_path.is_file():
        with detail_path.open(newline="", encoding="utf-8-sig") as f:
            existing_details = list(csv.DictReader(f))
    csv_complete = sum(x.get("status", "").lower() == "complete" for x in existing_details)
    csv_consistent = len(existing_details) == args.cycles and csv_complete == args.cycles
    if not incomplete and csv_consistent:
        print("JSON e CSV concordano: tutti i cicli completi; nessuna correzione necessaria.")
        return 0
    if not incomplete:
        print(f"JSON completo, ma web3_details.csv riporta {csv_complete}/{len(existing_details)}: rigenerazione CSV necessaria.")

    w3 = contract = None
    if incomplete:
        w3, contract = get_rpc_context(args.rpc, args.abi, args.address_file)
    if w3 is not None and state.get("contract", "").lower() != contract.address.lower():
        raise ValueError("L'indirizzo del contratto non coincide con web3_state.json")
    if int(state.get("chain_id", 0)) != 11155111:
        raise ValueError("chain_id diverso da Sepolia")
    wallet = str(state.get("wallet", ""))
    if not wallet:
        raise ValueError("wallet assente in web3_state.json")

    # Verifica tutte le posizioni PRIMA di apportare qualunque modifica.
    proofs = {}
    failed = []
    for c in incomplete:
        try:
            proof = inspect_cycle(w3, contract, wallet, c)
            proofs[int(c["attempt"])] = proof
            print(f"  VERIFICATO ON-CHAIN: ciclo {proof['attempt']} -> tokenId {proof['token_id']}, block {proof['block']}")
        except Exception as exc:
            failed.append((c.get("attempt", "?"), f"{type(exc).__name__}: {exc}"))
            print(f"  NON VERIFICATO: ciclo {c.get('attempt', '?')}: {type(exc).__name__}: {exc}")
    if failed:
        print("STOP: almeno un ciclo non è stato verificato. Nessun file modificato.")
        print("Controlla questi tentativi; non usare --apply finché non sono chiariti.")
        return 2
    print(f"Tutte le {len(proofs)} riconciliazioni sono verificabili.")
    if not args.apply:
        print("SOLO DIAGNOSTICA: nessun file modificato, nessuna transazione firmata o inviata.")
        print("Per riallineare SOLO lo stato locale: python reconcile_completed_cycles.py --out output --apply")
        return 0

    # Di questo punto in poi vengono scritti SOLO file locali.
    observations = load_obs(args.unity_observations, "token_id") if args.unity_observations else {}
    had_unity = any(str(r.get("unity_seed_equal", "")).strip() or str(r.get("unity_version_equal", "")).strip()
                    for r in existing_details)
    if had_unity and not args.unity_observations:
        raise ValueError("Sono presenti osservazioni Unity nel CSV: passa --unity-observations FILE prima di --apply")
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    backup = args.out / f"before_reconcile_{stamp}"
    backup.mkdir(parents=False, exist_ok=False)
    for filename in ("web3_state.json", "web3_details.csv", "web3_lifecycle.csv",
                     "web3_recovery_attempts.csv", "onchain_seed_manifest.csv"):
        f = args.out / filename
        if f.is_file():
            shutil.copy2(f, backup / filename)
    print(f"Backup JSON + CSV: {backup}")
    for c in incomplete:
        p = proofs[int(c["attempt"])]
        old_error = str(c.get("last_error", ""))
        if old_error:
            c["reconcile_previous_error"] = old_error
        c["claim_receipt_ok"] = True
        c["claim_block_number"] = p["block"]
        c["claim_fee_wei"] = p["fee_wei"]
        c["token_id"] = str(p["token_id"])
        c["claim_event_seed"] = p["seed"]
        c["chain_seed_equal"] = True
        c["chain_version_equal"] = True
        no_timing_stage(c, "claim", p["tx_hash"], p["block"])
        no_timing_stage(c, "import", p["tx_hash"], p["block"])
        no_timing_stage(c, "e2e", p["tx_hash"], p["block"])
        no_timing_stage(c, "chain_roundtrip", p["tx_hash"], p["block"])
        c["status"] = "complete"
        c["last_error"] = ""
        c["reconciled_after_batch"] = now()
        c["no_retrospective_latency"] = True

    # CSV ricostruiti dalle stesse informazioni utilizzate dal batch originale.
    # Il bridge Unity resta non osservato, senza inventare seed_equal/version_equal.
    persist(args.out, state, observations)
    print("STATO E CSV RICONCILIATI: completamenti on-chain verificati, latenze non misurate lasciate vuote.")
    print("Esegui nuovamente finalize_web3_latex.py; controlla campioni e report prima di pubblicare le tabelle.")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as exc:
        print(f"ERRORE: {type(exc).__name__}: {exc}", file=sys.stderr)
        sys.exit(2)
