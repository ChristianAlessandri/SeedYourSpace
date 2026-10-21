#!/usr/bin/env python3
"""Recupero controllato di un claim SYS/Sepolia rimasto signed_unknown.

Uso nella cartella del Blocco B:
  python recover_sys_claim.py --out output --attempt 8
  python recover_sys_claim.py --out output --attempt 8 --execute --accept-same-nonce-replacement

Prima esegue controlli SOLA LETTURA con DUE endpoint Sepolia indipendenti.
Con --execute firma UNA sola proposta alternativa di claim con il NONCE GIÀ
REGISTRATO (non un nuovo nonce e non un nuovo mint). Il file journal conserva
il raw firmato in modo da poterlo ritrasmettere IDENTICO dopo un crash.
Se la vecchia transazione risulta confermata, riconcilia lo stato con quella.
Per sicurezza non modifica automaticamente cicli diversi da --attempt.
"""
from __future__ import annotations
import argparse
import json
import os
import shutil
import sys
import time
from datetime import datetime, timezone
from decimal import Decimal
from pathlib import Path
from typing import Any

from sys_web3_common import CHAIN_ID, ROOT, get_rpc_context, load_json, save_json

WEI = 10**18
GWEI = 10**9
DEFAULT_RPC = "https://ethereum-sepolia-rpc.publicnode.com"
SECOND_RPC = "https://rpc.sepolia.org"


def stamp() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="seconds")


def fail(message: str) -> None:
    raise RuntimeError(message)


def tx_hash_str(v: Any) -> str:
    value = str(v or "")
    if not value:
        return ""
    return "0x" + value.removeprefix("0x").lower()


def load_dotenv() -> None:
    try:
        from dotenv import load_dotenv as fn
        fn(ROOT / ".env")
    except ImportError:
        pass


def read_inputs(args):
    path = args.out / "web3_state.json"
    state = load_json(path)
    if not isinstance(state, dict):
        fail("Stato web3_state.json assente o illeggibile")
    cycles = state.get("cycles", [])
    if not 1 <= args.attempt <= len(cycles):
        fail(f"Tentativo {args.attempt} assente nello stato locale ({len(cycles)} registrati)")
    c = cycles[args.attempt - 1]
    if int(c.get("attempt", -1)) != args.attempt:
        fail("Indice del ciclo non corrispondente al suo campo attempt")
    if c.get("status") == "complete":
        fail("Ciclo già completo; non eseguire operazioni di recupero")
    if not c.get("request_receipt_ok") or not c.get("request_id") or not c.get("vrf_tx_hash"):
        fail("Request e VRF non risultano entrambe verificate nel journal")
    if c.get("claim_receipt_ok"):
        fail("Claim già marcato come confermato: riprendere il batch normale")
    if not c.get("claim_tx_hash") or c.get("claim_nonce") is None:
        fail("Manca hash o nonce del claim originale: impossibile procedere automaticamente")
    if not c.get("seed") or not c.get("version"):
        fail("Seed/versione on-chain non disponibili nello stato del ciclo")
    if int(state.get("chain_id", 0)) != CHAIN_ID:
        fail("La chain_id del journal non è Sepolia")
    if not isinstance(state.get("wallet"), str) or not state["wallet"]:
        fail("Wallet mancante dallo stato locale")
    return path, state, c


def maybe_receipt(w3, hashvalue: str):
    from web3.exceptions import TransactionNotFound
    try:
        return w3.eth.get_transaction_receipt(hashvalue)
    except TransactionNotFound:
        return None


def maybe_tx(w3, hashvalue: str):
    from web3.exceptions import TransactionNotFound
    try:
        return w3.eth.get_transaction(hashvalue)
    except TransactionNotFound:
        return None


def read_node(rpc: str, args, state: dict, c: dict, journal: dict | None):
    w3, contract = get_rpc_context(rpc, args.abi, args.address_file)
    if contract.address.lower() != state["contract"].lower():
        fail("Indirizzo del contratto nello stato e nell'ABI non concordi")
    nonce = int(c["claim_nonce"])
    wallet = state["wallet"]
    latest = int(w3.eth.get_transaction_count(wallet, "latest"))
    pending = int(w3.eth.get_transaction_count(wallet, "pending"))
    request = contract.functions.vrfRequests(int(c["request_id"])).call()
    if len(request) != 4:
        fail("Risposta di vrfRequests inattesa")
    found = {}
    hashes = {"originale": tx_hash_str(journal["old_hash"] if journal else c["claim_tx_hash"])}
    if journal:
        hashes["alternativa"] = tx_hash_str(journal["new_hash"])
    for label, h in hashes.items():
        r = maybe_receipt(w3, h)
        t = maybe_tx(w3, h)
        found[label] = {
            "hash": h,
            "receipt": r,
            "tx": t,
            "status": None if r is None else int(r["status"]),
            "block": None if r is None else int(r["blockNumber"]),
        }
        if t is not None:
            if int(t["nonce"]) != nonce or t["from"].lower() != wallet.lower():
                fail(f"Transazione {label} associata a nonce/wallet inatteso")
    return {
        "w3": w3, "contract": contract, "block": int(w3.eth.block_number),
        "nonce_latest": latest, "nonce_pending": pending,
        "request": request, "found": found,
    }


def check_consensus(nodes, c: dict, state: dict, args, journal: dict | None):
    nonce = int(c["claim_nonce"])
    expected_id = int(c["request_id"])
    print(f"RequestId: {expected_id}\nClaim originale: {c['claim_tx_hash']}\nNonce originale: {nonce}")
    receipts = {}
    tx_present_pending = False
    for idx, n in enumerate(nodes, 1):
        r = n["request"]
        print(f"\nRPC {idx}: block={n['block']} latest_nonce={n['nonce_latest']} pending_nonce={n['nonce_pending']}")
        print("  vrfRequests: minter=", r[0], " version=", int(r[1]), " fulfilled=", bool(r[3]))
        for label, x in n["found"].items():
            if x["receipt"] is not None:
                print(f"  {label}: ricevuta status={x['status']} blocco={x['block']}")
                receipts.setdefault(label, []).append(x["receipt"])
            elif x["tx"] is not None:
                print(f"  {label}: visibile ma non ancora confermata")
                tx_present_pending = True
            else:
                print(f"  {label}: hash non visibile")
        if n["nonce_latest"] < nonce:
            fail("Nonce latest inferiore al nonce del claim: possibile transazione precedente mancante")
        if n["nonce_pending"] > nonce:
            tx_present_pending = True
    if abs(nodes[0]["block"] - nodes[1]["block"]) > 25:
        fail("RPC con altezza troppo discordante: attendere sincronia/controllare gli endpoint")

    winners = [(label, rs[0]) for label, rs in receipts.items()]
    if winners:
        if len(winners) > 1:
            fail("Entrambe le transazioni appaiono minate: verificare la coerenza degli RPC")
        label, receipt = winners[0]
        if int(receipt["status"]) != 1:
            fail(f"Claim {label} minato ma REVERTED: non modificare automaticamente lo stato")
        from web3.logs import DISCARD
        events = nodes[0]["contract"].events.SystemClaimed().process_receipt(receipt, errors=DISCARD)
        matching = [e for e in events if int(e["args"]["requestId"]) == expected_id
                    and str(e["args"]["owner"]).lower() == state["wallet"].lower()]
        if not matching:
            fail("Ricevuta senza evento SystemClaimed corrispondente: verifica manuale necessaria")
        return "mined", label
    if any(n["nonce_latest"] > nonce for n in nodes):
        fail("Nonce già consumato ma nessuna ricevuta riconosciuta: verificare la cronologia del wallet su Etherscan")
    if tx_present_pending:
        return "pending", None
    for n in nodes:
        if n["nonce_latest"] != nonce or n["nonce_pending"] != nonce:
            fail("Nonce non libero su tutti gli RPC; nessun invio possibile")
        req = n["request"]
        if (str(req[0]).lower() != state["wallet"].lower()
                or int(req[1]) != int(c["version"])
                or int(req[2]) != int(c["seed"])
                or not bool(req[3])):
            fail("VRF request non più claimabile o non coerente con wallet, versione e seed")
    return "free", None


def write_state(path: Path, state: dict):
    for attempt in range(8):
        try:
            save_json(path, state)
            return
        except PermissionError:
            if attempt == 7:
                raise
            time.sleep(0.5 * (attempt + 1))


def reconcile(path: Path, state: dict, c: dict, label: str, journal: dict | None):
    old = tx_hash_str(journal["old_hash"] if journal else c["claim_tx_hash"])
    if label == "alternativa":
        if journal is None:
            fail("Journal alternativa mancante")
        new = tx_hash_str(journal["new_hash"])
    else:
        new = old
    if not c.get("claim_rescue_original_tx_hash"):
        c["claim_rescue_original_tx_hash"] = old
    c["claim_tx_hash"] = new
    c["claim_broadcast_state"] = "mined_reconciled"
    c["claim_submitted_at"] = (journal["signed_at"] if label == "alternativa" else c.get("claim_submitted_at", stamp()))
    c["claim_receipt_ok"] = False  # il main importerà ufficialmente la receipt e registrerà il gas
    c["last_error"] = ""
    c["status"] = "vrf_fulfilled"
    c["claim_rescue_reconciled_at"] = stamp()
    c["claim_manual_recovery"] = True
    c["exclude_from_nominal_latency"] = True  # Annotazione metodologica: interruzione/reinvio fuori dal flusso nominale
    write_state(path, state)
    print("\nSTATO RIPRISTINATO: claim confermato e tracciato come", label)
    print("Adesso puoi rieseguire il comando ORIGINALE dei 30 cicli con la stessa --out.")


def send_replacement(args, state: dict, c: dict, journal_path: Path, journal: dict | None, node):
    from eth_account import Account
    from web3 import Web3
    from web3_cycle_audit import safe_fee, spent_wei
    key = os.environ.get("SYS_PRIVATE_KEY", "").strip()
    if not key:
        fail("SYS_PRIVATE_KEY mancante dal .env: non posso firmare")
    account = Account.from_key(key)
    del key
    if account.address.lower() != state["wallet"].lower():
        fail("Chiave privata non corrisponde al wallet del journal")
    w3, contract = node["w3"], node["contract"]
    if journal is None:
        fee = safe_fee(w3, int(args.max_fee_gwei * GWEI))
        nonce = int(c["claim_nonce"])
        tx = {"chainId": CHAIN_ID, "from": account.address, "nonce": nonce, "value": 0, **fee}
        func = contract.functions.claimSystem(int(c["request_id"]))
        gas = int(func.estimate_gas(tx))
        tx["gas"] = (gas * 120 + 99) // 100
        fee_cap = int(fee.get("maxFeePerGas", fee.get("gasPrice")))
        if int(tx["gas"]) * fee_cap + spent_wei(state) > int(args.max_total_eth * WEI):
            fail("La nuova transazione supererebbe il cap di spesa totale indicato")
        if int(w3.eth.get_balance(account.address)) < int(tx["gas"]) * fee_cap:
            fail("Saldo del wallet insufficiente a coprire il gas")
        signed = account.sign_transaction(func.build_transaction(tx))
        raw_hex = signed.raw_transaction.hex()
        new_hash = "0x" + w3.keccak(signed.raw_transaction).hex().removeprefix("0x")
        journal = {
            "attempt": args.attempt, "chain_id": CHAIN_ID, "wallet": account.address,
            "contract": contract.address, "request_id": str(c["request_id"]),
            "nonce": nonce, "old_hash": tx_hash_str(c["claim_tx_hash"]),
            "new_hash": new_hash, "raw_hex": raw_hex,
            "signed_at": stamp(), "max_fee_gwei": str(args.max_fee_gwei), "broadcast": "unknown",
        }
        # Journal PRIMA del broadcast: ogni successivo retry invia gli stessi byte.
        save_json(journal_path, journal)
        print("\nJournal creato:", journal_path)
    else:
        if str(journal["request_id"]) != str(c["request_id"]) or int(journal["nonce"]) != int(c["claim_nonce"]):
            fail("Il journal di recupero non corrisponde al ciclo selezionato")
        if tx_hash_str(c["claim_tx_hash"]) not in (tx_hash_str(journal["old_hash"]), tx_hash_str(journal["new_hash"])):
            fail("L'hash claim nello stato non corrisponde a nessuno dei due hash riconosciuti")
        if Account.recover_transaction(bytes.fromhex(journal["raw_hex"].removeprefix("0x"))).lower() != account.address.lower():
            fail("La transazione salvata non appartiene al wallet del batch")
        raw_bytes = bytes.fromhex(journal["raw_hex"].removeprefix("0x"))
        if tx_hash_str(Web3.keccak(raw_bytes).hex()) != tx_hash_str(journal["new_hash"]):
            fail("Hash firmato del journal alterato")
        print("\nRipresa del journal esistente; nessuna nuova firma/nonce")
    raw = bytes.fromhex(journal["raw_hex"].removeprefix("0x"))
    print("Claim alternativo, STESSO nonce:", journal["new_hash"])
    try:
        sent = w3.eth.send_raw_transaction(raw)
        if tx_hash_str(sent.hex()) != tx_hash_str(journal["new_hash"]):
            fail("RPC ha restituito un hash diverso da quello firmato; journal conservato")
        print("Broadcast RPC:", sent.hex())
        journal["broadcast"] = "accepted"
        save_json(journal_path, journal)
    except Exception as exc:
        # Es. 'already known' o 'replacement transaction underpriced'. NON
        # cancellare il journal e NON modificare lo stato originale.
        print(f"Broadcast incerto: {type(exc).__name__}: {exc}")
        print("Journal conservato; prima di un altro intervento riesegui questa stessa diagnostica.")
    print("Controllo conferma dei due possibili hash (il vecchio e l'alternativo)...")
    expiry = time.monotonic() + args.timeout
    while time.monotonic() < expiry:
        for label, h in (("originale", journal["old_hash"]), ("alternativa", journal["new_hash"])):
            r = maybe_receipt(w3, h)
            if r is not None:
                if int(r["status"]) != 1:
                    fail(f"Il claim {label} risulta minato con REVERT: richiede verifica manuale")
                # Riesegue controlli completi su due RPC prima di modificare lo stato.
                nodes = [read_node(u, args, state, c, journal) for u in (args.rpc, args.rpc2)]
                status, winner = check_consensus(nodes, c, state, args, journal)
                if status == "mined":
                    reconcile(args.out / "web3_state.json", state, c, winner, journal)
                    return
        time.sleep(args.poll)
    print("Nessuna receipt entro il timeout. Mantieni sia web3_state.json sia il journal,")
    print("e riesegui QUESTO script, non il batch, per riconciliare i due possibili hash.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", type=Path, default=Path("output"))
    parser.add_argument("--attempt", type=int, default=8)
    parser.add_argument("--rpc", default=os.environ.get("SYS_RPC_URL", DEFAULT_RPC))
    parser.add_argument("--rpc2", default=SECOND_RPC, help="Secondo RPC Sepolia indipendente")
    parser.add_argument("--abi", type=Path, default=ROOT / "resources/smart_contract_abi.json")
    parser.add_argument("--address-file", type=Path, default=ROOT / "resources/smart_contract_address.txt")
    parser.add_argument("--max-fee-gwei", type=Decimal, default=Decimal("20"))
    parser.add_argument("--max-total-eth", type=Decimal, default=Decimal("0.06"))
    parser.add_argument("--timeout", type=int, default=420)
    parser.add_argument("--poll", type=float, default=5)
    parser.add_argument("--execute", action="store_true")
    parser.add_argument("--accept-same-nonce-replacement", action="store_true")
    load_dotenv()
    args = parser.parse_args()
    path, state, c = read_inputs(args)
    journal_path = args.out / f"rescue_claim_{args.attempt:02d}.json"
    journal = load_json(journal_path)
    if journal is not None:
        if (int(journal.get("attempt", -1)) != args.attempt or int(journal.get("chain_id", 0)) != CHAIN_ID
                or journal.get("wallet", "").lower() != state["wallet"].lower()
                or journal.get("contract", "").lower() != state["contract"].lower()):
            fail("Journal di recupero incompatibile con il batch")
        print("Journal di recupero esistente:", journal_path)
    print("Diagnostica incrociata della transazione claim; NESSUN nuovo mint.")
    if args.rpc.rstrip("/").lower() == args.rpc2.rstrip("/").lower():
        fail("I due URL RPC devono essere differenti")
    nodes = [read_node(u, args, state, c, journal) for u in (args.rpc, args.rpc2)]
    state_kind, winner = check_consensus(nodes, c, state, args, journal)
    if state_kind == "mined":
        print("Una transazione claim è già stata MINATA.")
        if args.execute and args.accept_same_nonce_replacement:
            # Mai inviare un'altra transazione se ne esiste già una minata.
            reconcile(path, state, c, winner, journal)
        else:
            print("Per sincronizzare web3_state.json puoi usare --execute --accept-same-nonce-replacement (NESSUN broadcast in questo caso).")
        return
    if state_kind == "pending":
        print("STOP: un RPC vede una transazione pendente con lo stesso nonce; attendi o verifica su explorer.")
        return
    print("\nEntrambi i nodi vedono nonce libero e richiesta VRF claimabile.")
    if not args.execute:
        print("SOLO DIAGNOSTICA: nessuna chiave privata letta, nessuna firma/broadcast.")
        print("Se vuoi riprendere con una transazione ALTERNATIVA allo stesso nonce:")
        print("  python recover_sys_claim.py --out output --attempt 8 --execute --accept-same-nonce-replacement")
        return
    if not args.accept_same_nonce_replacement:
        fail("L'invio richiede il consenso esplicito: --accept-same-nonce-replacement")
    if journal is None:
        backup = args.out / f"web3_state.before_claim_rescue_{args.attempt:02d}.json"
        if not backup.exists():
            shutil.copy2(path, backup)
            print("Backup dello stato:", backup)
    send_replacement(args, state, c, journal_path, journal, nodes[0])


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print("STOP SICURO:", type(exc).__name__, exc, file=sys.stderr)
        sys.exit(2)
