#!/usr/bin/env python3
"""SYS/Sepolia: 30 cicli reali Request -> VRF -> Claim con ripresa idempotente.

Default: solo preflight RPC (NESSUNA TRANSAZIONE).
La modalità --execute richiede anche --acknowledge-cost e un wallet di prova.
Se cade RPC/processo, rilanciare con --execute --acknowledge-cost:
le transazioni già firmate non vengono mai sostituite/inviate di nuovo.
"""
from __future__ import annotations
import argparse
import os
import sys
import time
from datetime import datetime, timezone
from decimal import Decimal
from pathlib import Path
from typing import Any
from sys_web3_common import (
    ROOT, CHAIN_ID, LIFECYCLE_COLS, DETAIL_COLS, now, to_seconds,
    block_iso, load_json, save_json, save_csv, load_obs,
    get_rpc_context, get_event_range, event_in_receipt, ints_equal,
)

DEFAULT_RPC = "https://ethereum-sepolia-rpc.publicnode.com"
GWEI = 10 ** 9
WEI = 10 ** 18
ZERO = "0x" + "0" * 40


def arguments():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--rpc", default=os.environ.get("SYS_RPC_URL", DEFAULT_RPC))
    p.add_argument("--abi", type=Path, default=ROOT / "resources/smart_contract_abi.json")
    p.add_argument("--address-file", type=Path, default=ROOT / "resources/smart_contract_address.txt")
    p.add_argument("--out", type=Path, default=ROOT / "output")
    p.add_argument("--cycles", type=int, default=30)
    p.add_argument("--recoveries", type=int, default=10)
    p.add_argument("--execute", action="store_true", help="autorizza firma e broadcast sul testnet")
    p.add_argument("--acknowledge-cost", action="store_true", help="presa visione di 30 * mintPrice + gas + LINK della subscription")
    p.add_argument("--max-total-eth", type=Decimal, default=Decimal("0.06"), help="limite massimo di ETH stimati/consumati nel batch")
    p.add_argument("--max-fee-gwei", type=Decimal, default=Decimal("20"), help="massimo maxFeePerGas permesso")
    p.add_argument("--poll-vrf", type=float, default=12)
    p.add_argument("--poll-request", type=float, default=4)
    p.add_argument("--poll-claim", type=float, default=3)
    p.add_argument("--receipt-timeout", type=float, default=420)
    p.add_argument("--vrf-timeout", type=float, default=2400)
    p.add_argument("--disconnect-seconds", type=float, default=2, help="interruzione intenzionale nelle prove di recovery")
    p.add_argument("--unity-observations", type=Path, default=None, help="CSV token_id,seed,algorithm_version osservati da Unity")
    return p.parse_args()


def stage(start, end, success, tx_hash="", block_number="", **extra):
    d = dict(start_time=start or "", end_time=end or "", elapsed_s=(round(to_seconds(start, end), 6) if start and end else ""), completed=bool(success), tx_hash=tx_hash or "", block_number=block_number or "")
    d.update(extra)
    return d


def persist(out: Path, state: dict, observations: dict):
    save_json(out / "web3_state.json", state)
    rows, details, seeds, recovery_attempts = [], [], [], []
    for c in state["cycles"]:
        rid = str(c.get("request_id", "") or f"attempt:{c['attempt']}")
        seed_match = None
        version_match = None
        if str(c.get("token_id", "")) in observations:
            o = observations[str(c["token_id"])]
            staged_ok = str(o.get("staged_equal", "true")).lower() == "true"
            seed_match = staged_ok and ints_equal(o["seed"], c["seed"])
            version_match = staged_ok and ints_equal(o["algorithm_version"], c["version"])
        for phase, d in c.get("stages", {}).items():
            rows.append({"request_id": rid, "phase": phase, **d,
                "seed_equal": ("" if seed_match is None else str(seed_match).lower()),
                "version_equal": ("" if version_match is None else str(version_match).lower()),
                "recovery_scenario": ("reconnect_last_5000" if phase == "recovery" else "")})
        for i, entry in enumerate(c.get("recovery_attempts", []), 1):
            recovery_attempts.append({
                "request_id": rid, "attempt_number": i,
                "start_time": entry["start_time"], "end_time": entry["end_time"],
                "elapsed_s": entry["elapsed_s"],
                "completed": str(entry["completed"]).lower(),
                "latest_block": entry.get("latest_block", ""),
                "last_request_id": entry.get("last_request_id", ""),
                "error": entry.get("error", "")})
        details.append({"attempt": c["attempt"], "request_id": c.get("request_id", ""),
            "request_tx_hash": c.get("request_tx_hash", ""), "vrf_tx_hash": c.get("vrf_tx_hash", ""),
            "claim_tx_hash": c.get("claim_tx_hash", ""), "token_id": c.get("token_id", ""),
            "seed": c.get("seed", ""), "algorithm_version": c.get("version", ""),
            "chain_seed_equal": c.get("chain_seed_equal", ""),
            "chain_version_equal": c.get("chain_version_equal", ""),
            "unity_seed_equal": ("" if seed_match is None else int(seed_match)),
            "unity_version_equal": ("" if version_match is None else int(version_match)),
            "status": c.get("status", ""), "error": c.get("last_error", "")})
        if c.get("token_id") is not None and c.get("seed") is not None:
            seeds.append({"request_id": rid, "token_id": c["token_id"],
                          "seed": c["seed"], "algorithm_version": c["version"]})
    save_csv(out / "web3_lifecycle.csv", LIFECYCLE_COLS, rows)
    save_csv(out / "web3_recovery_attempts.csv",
             ["request_id", "attempt_number", "start_time", "end_time", "elapsed_s",
              "completed", "latest_block", "last_request_id", "error"],
             recovery_attempts)
    save_csv(out / "web3_details.csv", DETAIL_COLS, details)
    save_csv(out / "onchain_seed_manifest.csv", ["request_id", "token_id", "seed", "algorithm_version"], seeds)


def receipt_until(w3, tx_hash: str, every: float, timeout: float):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        try:
            receipt = w3.eth.get_transaction_receipt(tx_hash)
            if receipt:
                return receipt, now()
        except Exception as exc:
            # TransactionNotFound è fisiologico; errori RPC temporanei sono registrati dal chiamante a timeout.
            if "not found" not in str(exc).lower() and "notfound" not in type(exc).__name__.lower():
                print("[RPC receipt]", type(exc).__name__, file=sys.stderr)
        time.sleep(every)
    raise TimeoutError(f"Ricevuta assente entro {timeout:.0f}s: {tx_hash}; NON reinviare la transazione")


def safe_fee(w3, max_fee_wei: int):
    latest = w3.eth.get_block("latest")
    base = latest.get("baseFeePerGas")
    if base is None:
        fee = int(w3.eth.gas_price)
        if fee > max_fee_wei:
            raise RuntimeError("gasPrice supera --max-fee-gwei")
        return {"gasPrice": fee}
    try:
        priority = int(w3.eth.max_priority_fee)
    except Exception:
        priority = int(w3.to_wei(1, "gwei"))
    priority = max(1, priority)
    max_fee = int(base) * 2 + priority
    if max_fee > max_fee_wei:
        raise RuntimeError(f"maxFeePerGas {Decimal(max_fee)/GWEI} gwei supera il limite")
    return {"type": 2, "maxFeePerGas": max_fee, "maxPriorityFeePerGas": priority}


def spent_wei(state):
    return sum(int(c.get("request_value_wei", 0)) * int(bool(c.get("request_receipt_ok")))
        + int(c.get("request_fee_wei", 0)) + int(c.get("claim_fee_wei", 0))
        for c in state["cycles"])


def send_once(w3, contract, account, func, value: int, c: dict, prefix: str,
              save, max_fee_wei: int, max_total_wei: int, state: dict):
    tx_key = prefix + "_tx_hash"
    if c.get(tx_key):
        return c[tx_key]  # Nessun nuovo nonce e nessun nuovo broadcast.
    fee = safe_fee(w3, max_fee_wei)
    tx = {"chainId": CHAIN_ID, "from": account.address, "nonce": int(w3.eth.get_transaction_count(account.address, "pending")),
          "value": value, **fee}
    gas_estimate = int(func.estimate_gas(tx))
    tx["gas"] = (gas_estimate * 120 + 99) // 100
    maximum_fee = tx["gas"] * int(fee.get("maxFeePerGas", fee.get("gasPrice")))
    if spent_wei(state) + value + maximum_fee > max_total_wei:
        raise RuntimeError("Limite ETH batch raggiunto; richiesta NON inviata")
    if int(w3.eth.get_balance(account.address)) < value + maximum_fee:
        raise RuntimeError("Saldo del wallet insufficiente per valore + massimo gas")
    signed = account.sign_transaction(func.build_transaction(tx))
    expected = w3.keccak(signed.raw_transaction).hex()
    c[tx_key] = expected
    c[prefix + "_submitted_at"] = now()
    c[prefix + "_nonce"] = tx["nonce"]
    c[prefix + "_max_fee_wei"] = maximum_fee
    c[prefix + "_broadcast_state"] = "signed_unknown"
    save()  # Persistenza PRIMA dell'invio: evita pagamenti duplicati dopo crash.
    try:
        got = w3.eth.send_raw_transaction(signed.raw_transaction).hex()
        if got.lower() != expected.lower():
            raise RuntimeError("RPC ha restituito un tx hash diverso da quello firmato")
        c[prefix + "_broadcast_state"] = "sent"
        save()
    except Exception as exc:
        c[prefix + "_broadcast_state"] = "unknown_after_error"
        save()
        raise RuntimeError(f"Broadcast incerto ({type(exc).__name__}). Controllare {expected} su explorer; NON creare nuova transazione") from exc
    return expected


def after_receipt(w3, contract, c: dict, prefix: str, seconds: float, timeout: float, save):
    receipt_key = prefix + "_receipt_ok"
    if c.get(receipt_key):
        return w3.eth.get_transaction_receipt(c[prefix + "_tx_hash"])
    r, end_time = receipt_until(w3, c[prefix + "_tx_hash"], seconds, timeout)
    good = int(r.get("status", 0)) == 1
    c[receipt_key] = good
    c[prefix + "_receipt_time"] = end_time
    c[prefix + "_block_number"] = int(r["blockNumber"])
    c[prefix + "_fee_wei"] = int(r["gasUsed"]) * int(r.get("effectiveGasPrice", 0))
    c["stages"][prefix] = stage(c[prefix + "_submitted_at"], end_time, good,
                                 c[prefix + "_tx_hash"], int(r["blockNumber"]))
    save()
    if not good:
        raise RuntimeError(f"{prefix} revert (status=0); tx: {c[prefix + '_tx_hash']}")
    return r


def recovery_check(args, c: dict, address: str, save):
    """Replica la ricerca negli ultimi 5000 blocchi su una nuova connessione RPC.

    Registra *ogni tentativo*, inclusi i fallimenti transitori: un retry
    riuscito non cancella il fallimento precedente. Non simula la UI Unity.
    """
    if args.disconnect_seconds:
        time.sleep(args.disconnect_seconds)
    began = now()
    latest = None
    last_id = None
    w3 = co = None
    matched = False
    error = ""
    try:
        w3, co = get_rpc_context(args.rpc, args.abi, args.address_file)
        latest = int(w3.eth.block_number)
        logs = get_event_range(co, "SystemRequested", max(latest - 5000, 0), latest,
                               {"requester": address})
        logs.sort(key=lambda e: (e["blockNumber"], e["transactionIndex"], e["logIndex"]))
        last_id = int(logs[-1]["args"]["requestId"]) if logs else None
        matched = last_id == int(c["request_id"])
        if matched:
            data = co.functions.vrfRequests(last_id).call()
            matched = str(data[0]).lower() == address.lower() and str(data[0]).lower() != ZERO.lower()
        if not matched:
            error = "Ultima richiesta storica diversa, assente o già eliminata"
    except Exception as exc:
        error = f"{type(exc).__name__}: {exc}"
    finished = now()
    measure = stage(began, finished, matched, block_number=latest or "")
    c["stages"]["recovery"] = measure  # Stato corrente / ultimo tentativo
    c["recovery_last_request_id"] = last_id
    c.setdefault("recovery_attempts", []).append({
        "start_time": began, "end_time": finished,
        "elapsed_s": measure["elapsed_s"], "completed": matched,
        "latest_block": latest if latest is not None else "",
        "last_request_id": last_id if last_id is not None else "",
        "error": error})
    save()
    if not matched:
        raise RuntimeError(f"Recovery fallita: {error}. Rieseguire lo STESSO comando")
    return w3, co


def vrf_wait(w3, contract, c: dict, args, save):
    if c.get("vrf_tx_hash"):
        return
    rid = int(c["request_id"])
    deadline = time.monotonic() + args.vrf_timeout
    while time.monotonic() < deadline:
        try:
            data = contract.functions.vrfRequests(rid).call()
            if bool(data[3]):
                seen = now()
                latest = int(w3.eth.block_number)
                logs = get_event_range(contract, "RandomnessArrived", int(c["request_block_number"]),
                                       latest, {"requestId": rid})
                if not logs:
                    raise RuntimeError("isFulfilled=true senza RandomnessArrived nel range richiesto")
                event = logs[-1]
                vrf_block = int(event["blockNumber"])
                req_block_time = int(w3.eth.get_block(int(c["request_block_number"]))["timestamp"])
                vrf_block_time = int(w3.eth.get_block(vrf_block)["timestamp"])
                request_block_iso = block_iso(req_block_time)
                vrf_block_iso = block_iso(vrf_block_time)
                c["seed"] = str(int(event["args"]["seed"]))
                c["vrf_tx_hash"] = event["transactionHash"].hex()
                c["vrf_block_number"] = vrf_block
                c["version"] = str(int(data[1]))
                c["chain_seed_equal"] = ints_equal(data[2], event["args"]["seed"])
                c["chain_version_equal"] = ints_equal(data[1], c["request_algorithm_version"])
                c["stages"]["vrf"] = stage(request_block_iso, vrf_block_iso, True, c["vrf_tx_hash"], vrf_block)
                delay = to_seconds(vrf_block_iso, seen)
                c["stages"]["poll"] = stage(vrf_block_iso, seen, delay >= 0, c["vrf_tx_hash"], vrf_block)
                if delay < 0:
                    c["stages"]["poll"]["elapsed_s"] = ""  # Skew locale: non nascondere il problema.
                    print("[AVVISO] Orologio locale non allineato al timestamp del blocco", file=sys.stderr)
                save()
                if not c["chain_seed_equal"] or not c["chain_version_equal"]:
                    raise RuntimeError("Divergenza tra RandomnessArrived, vrfRequests e versione di SystemRequested")
                return
        except Exception as exc:
            print(f"[VRF {rid}] polling RPC: {type(exc).__name__}: {exc}", file=sys.stderr)
        time.sleep(args.poll_vrf)
    raise TimeoutError(f"VRF non osservata entro {args.vrf_timeout}s per {rid}; recuperabile alla prossima esecuzione")


def claim_import(w3, contract, account, c: dict, args, state, save, fee_limit, total_limit):
    if not c.get("claim_tx_hash"):
        c["owner_balance_before"] = int(contract.functions.balanceOf(account.address).call())
        c["claim_storage_case"] = "claim:balance0" if c["owner_balance_before"] == 0 else "claim:balance_positive"
        save()
    send_once(w3, contract, account, contract.functions.claimSystem(int(c["request_id"])), 0,
              c, "claim", save, fee_limit, total_limit, state)
    claim_receipt = after_receipt(w3, contract, c, "claim", args.poll_claim, args.receipt_timeout, save)
    event = event_in_receipt(contract, "SystemClaimed", claim_receipt, requestId=int(c["request_id"]))
    c["token_id"] = str(int(event["tokenId"]))
    c["claim_event_seed"] = str(int(event["seed"]))
    c["chain_seed_equal"] = bool(c["chain_seed_equal"]) and ints_equal(event["seed"], c["seed"])
    if str(event["owner"]).lower() != account.address.lower():
        raise RuntimeError("Owner evento SystemClaimed errato")
    save()
    if "import" in c.get("stages", {}):
        return
    started = now()
    token_id = int(c["token_id"])
    chain_seed, chain_version = contract.functions.systems(token_id).call()
    owner = contract.functions.ownerOf(token_id).call()
    balance = int(contract.functions.balanceOf(account.address).call())
    visible = any(int(contract.functions.tokenOfOwnerByIndex(account.address, i).call()) == token_id
                  for i in range(balance))
    svg = contract.functions.previewSVG(int(chain_seed)).call()
    c["chain_seed_equal"] = bool(c["chain_seed_equal"]) and ints_equal(chain_seed, c["seed"])
    c["chain_version_equal"] = bool(c["chain_version_equal"]) and ints_equal(chain_version, c["version"])
    good = (owner.lower() == account.address.lower() and visible and bool(svg)
            and c["chain_seed_equal"] and c["chain_version_equal"])
    end = now()
    c["stages"]["import"] = stage(started, end, good, c["claim_tx_hash"], int(claim_receipt["blockNumber"]))
    c["stages"]["e2e"] = stage(c["request_submitted_at"], end, good, c["claim_tx_hash"], int(claim_receipt["blockNumber"]))
    # Questa fase testa l'identità numerica on-chain e la conversione Python;
    # la prova effettiva di Nethereum/Unity richiede un CSV di osservazioni esterne.
    c["stages"]["chain_roundtrip"] = stage(started, end, good, c["claim_tx_hash"], int(claim_receipt["blockNumber"]))
    c["status"] = "complete" if good else "integrity_failure"
    save()
    if not good:
        raise RuntimeError("Invariante dell'importazione violata")


def main():
    try:
        from dotenv import load_dotenv
        load_dotenv(ROOT / ".env")
    except ImportError:
        pass
    args = arguments()
    if args.cycles < 1 or not (0 <= args.recoveries <= args.cycles):
        raise ValueError("Impostare cycles>=1 e 0<=recoveries<=cycles")
    if args.max_total_eth <= 0 or args.max_fee_gwei <= 0:
        raise ValueError("Limiti di spesa positivi obbligatori")
    if min(args.poll_vrf, args.poll_request, args.poll_claim) <= 0:
        raise ValueError("Intervalli di polling positivi obbligatori")
    w3, contract = get_rpc_context(args.rpc, args.abi, args.address_file)
    print("RPC Sepolia OK | contratto:", contract.address)
    mint_price = int(contract.functions.mintPrice().call())
    version = int(contract.functions.algorithmVersion().call())
    pending_cap = int(contract.functions.maxPendingRequests().call())
    balance_needed = Decimal(mint_price * args.cycles) / WEI
    print(f"Prezzo: {Decimal(mint_price)/WEI} ETH × {args.cycles} = {balance_needed} ETH (+ gas, LINK subscription indipendente)")
    print(f"Versione algoritmo: {version} | maxPendingRequests: {pending_cap} | fee cap: {args.max_fee_gwei} gwei")
    if not args.execute:
        print("DRY RUN: nessuna chiave letta, nessuna transazione firmata o inviata.")
        return
    if not args.acknowledge_cost:
        raise RuntimeError("Per transazioni REALI su Sepolia è obbligatorio --acknowledge-cost")
    pk = os.getenv("SYS_PRIVATE_KEY", "").strip()
    if not pk:
        raise RuntimeError("SYS_PRIVATE_KEY mancante da .env o ambiente")
    from eth_account import Account
    account = Account.from_key(pk)
    del pk
    state_path = args.out / "web3_state.json"
    existing = load_json(state_path)
    if existing:
        state = existing
        if state.get("chain_id") != CHAIN_ID or state.get("contract", "").lower() != contract.address.lower() or state.get("wallet", "").lower() != account.address.lower():
            raise RuntimeError("Stato persistente relativo a wallet/contratto/rete differente")
        if state["target_cycles"] != args.cycles or state["target_recoveries"] != args.recoveries:
            raise RuntimeError("Non variare --cycles e --recoveries durante un batch; usare nuova --out")
    else:
        state = dict(chain_id=CHAIN_ID, contract=contract.address, wallet=account.address,
                     target_cycles=args.cycles, target_recoveries=args.recoveries,
                     created_at=now(), mint_price_wei=mint_price, cycles=[])
    if int(state["mint_price_wei"]) != mint_price:
        raise RuntimeError("mintPrice modificato dall'inizio del batch: controllare manualmente")
    observations = load_obs(args.unity_observations, "token_id") if args.unity_observations else {}
    def save():
        persist(args.out, state, observations)
    save()
    max_fee_wei = int(args.max_fee_gwei * GWEI)
    max_total_wei = int(args.max_total_eth * WEI)
    for index in range(args.cycles):
        if index == len(state["cycles"]):
            pending = int(contract.functions.pendingRequestsByUser(account.address).call())
            if pending:
                raise RuntimeError(f"Wallet ha {pending} richieste pendenti non tracciate. Risolvere prima di creare una nuova richiesta.")
            c = dict(attempt=index + 1, status="created", stages={},
                     request_value_wei=mint_price, request_storage_case="request:pending0")
            state["cycles"].append(c)
            save()
        else:
            c = state["cycles"][index]
        if c.get("status") == "complete":
            print(f"[{index+1}/{args.cycles}] già completo")
            continue
        print(f"[{index+1}/{args.cycles}] request_id={c.get('request_id','da creare')}", flush=True)
        try:
            send_once(w3, contract, account, contract.functions.requestSystemMint(), mint_price,
                      c, "request", save, max_fee_wei, max_total_wei, state)
            r = after_receipt(w3, contract, c, "request", args.poll_request, args.receipt_timeout, save)
            if not c.get("request_id"):
                event = event_in_receipt(contract, "SystemRequested", r, requester=account.address)
                c["request_id"] = str(int(event["requestId"]))
                c["request_algorithm_version"] = str(int(event["algorithmVersion"]))
                c["request_block_number"] = int(r["blockNumber"])
                c["status"] = "request_mined"
                save()
            if index < args.recoveries and not c["stages"].get("recovery", {}).get("completed", False):
                w3, contract = recovery_check(args, c, account.address, save)
            vrf_wait(w3, contract, c, args, save)
            c["status"] = "vrf_fulfilled"
            save()
            claim_import(w3, contract, account, c, args, state, save, max_fee_wei, max_total_wei)
            c["last_error"] = ""
            save()
            print(f"  OK | tokenId {c['token_id']} | seed {c['seed'][:16]}…")
        except Exception as exc:
            # Mantiene nel CSV anche i tentativi incompleti (denominatore successo).
            if not c.get("request_receipt_ok"):
                failing_phase = "request"
            elif index < args.recoveries and not c["stages"].get("recovery", {}).get("completed", False):
                failing_phase = "recovery"
            elif not c.get("vrf_tx_hash"):
                failing_phase = "vrf"
            elif not c.get("claim_receipt_ok"):
                failing_phase = "claim"
            else:
                failing_phase = "import"
            c["stages"].setdefault(failing_phase,
                stage(c.get(failing_phase + "_submitted_at", ""), now(), False,
                      c.get(failing_phase + "_tx_hash", ""), ""))
            c["last_error"] = f"{type(exc).__name__}: {exc}"
            if c.get("status") != "complete":
                c["status"] = "paused"
            save()
            print(f"Interrotto in sicurezza: {c['last_error']}", file=sys.stderr)
            print("Rieseguire LO STESSO comando: riprenderà la richiesta/traccia già esistente.", file=sys.stderr)
            return 2
    print("BATCH COMPLETATO:", args.out / "web3_lifecycle.csv")
    if not observations:
        print("NOTA: seed_equal/version_equal Unity restano vuoti finché non si forniscono osservazioni del bridge.")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as ex:
        print(f"ERRORE: {type(ex).__name__}: {ex}", file=sys.stderr)
        sys.exit(1)
