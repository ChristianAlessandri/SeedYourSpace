#!/usr/bin/env python3
"""Diagnostica SOLA LETTURA di uno stato SYS/Sepolia.

Richiede web3.py e, facoltativamente, python-dotenv. Non legge chiavi private,
non modifica i CSV, non firma e non invia transazioni.

Esempio: python check_sepolia_resume.py --out output --attempt 8
"""
from __future__ import annotations
import argparse
import json
import os
import time
from pathlib import Path


def load_state(path: Path) -> dict:
    last = None
    for _ in range(5):
        try:
            with path.open(encoding="utf-8-sig") as handle:
                return json.load(handle)
        except (PermissionError, json.JSONDecodeError, OSError) as exc:
            last = exc
            time.sleep(0.25)
    raise RuntimeError(f"Impossibile leggere {path}: {last}")


def main() -> None:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--out", type=Path, default=Path("output"))
    p.add_argument("--attempt", type=int, default=8)
    p.add_argument("--rpc", default=None, help="RPC Sepolia alternativo (facoltativo)")
    args = p.parse_args()
    try:
        from web3 import Web3
        from web3.exceptions import TransactionNotFound
    except ImportError as exc:
        raise SystemExit("Installa web3.py: python -m pip install 'web3>=7,<8'") from exc
    try:
        from dotenv import load_dotenv
        load_dotenv(Path(__file__).resolve().parent / ".env")
    except ImportError:
        pass

    state = load_state(args.out / "web3_state.json")
    cycles = state.get("cycles", [])
    if not 1 <= args.attempt <= len(cycles):
        raise SystemExit(f"Tentativo {args.attempt} assente. Presenti: {len(cycles)}")
    c = cycles[args.attempt - 1]
    print(f"Tentativo: {args.attempt}/{state.get('target_cycles', '?')}")
    print("Stato locale:", c.get("status", "non definito"))
    print("request_id:", c.get("request_id", "non assegnato"))
    print("token_id:", c.get("token_id", "non assegnato"))
    print("Ultimo errore:", c.get("last_error", "nessuno"))

    url = args.rpc or os.getenv("SYS_RPC_URL") or "https://ethereum-sepolia-rpc.publicnode.com"
    w3 = Web3(Web3.HTTPProvider(url, request_kwargs={"timeout": 20}))
    if w3.eth.chain_id != 11155111:
        raise SystemExit("RPC errato: chain_id != 11155111 (Sepolia)")
    print("\nRPC verificato: Sepolia (11155111)")
    for prefix in ("request", "claim"):
        tx_hash = c.get(prefix + "_tx_hash")
        print(f"\n{prefix.upper()}: {tx_hash or 'nessun hash registrato'}")
        if not tx_hash:
            continue
        print("  receipt registrata:", c.get(prefix + "_receipt_ok", False))
        print("  nonce registrato:", c.get(prefix + "_nonce", "?"))
        print("  broadcast:", c.get(prefix + "_broadcast_state", "?"))
        try:
            receipt = w3.eth.get_transaction_receipt(tx_hash)
            print("  RECEIPT PRESENTE | status:", int(receipt["status"]),
                  "| block:", int(receipt["blockNumber"]),
                  "| gasUsed:", int(receipt["gasUsed"]))
            if int(receipt["status"]) == 0:
                print("  ATTENZIONE: transazione inclusa ma reverted. Fermarsi e indagare.")
        except TransactionNotFound:
            print("  RECEIPT NON PRESENTE su questo endpoint")
        except Exception as exc:
            print("  Errore query receipt:", type(exc).__name__, str(exc)[:180])
        try:
            tx = w3.eth.get_transaction(tx_hash)
            print("  TRANSAZIONE PRESENTE | block:",
                  ("pending" if tx["blockNumber"] is None else int(tx["blockNumber"])),
                  "| nonce:", int(tx["nonce"]))
        except TransactionNotFound:
            print("  TRANSAZIONE NON VISIBILE su questo endpoint")
        except Exception as exc:
            print("  Errore query tx:", type(exc).__name__, str(exc)[:180])

    address = state.get("wallet")
    if address:
        try:
            print("\nNonce del wallet:")
            print("  latest:", w3.eth.get_transaction_count(address, "latest"))
            print("  pending:", w3.eth.get_transaction_count(address, "pending"))
        except Exception as exc:
            print("  Impossibile leggere nonce:", type(exc).__name__, str(exc)[:180])
    complete = sum(cycle.get("status") == "complete" for cycle in cycles)
    print(f"\nCicli completi registrati: {complete}/{state.get('target_cycles', '?')}")
    print("Diagnostica sola lettura completata; nessuna transazione inviata.")


if __name__ == "__main__":
    main()
