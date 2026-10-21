"""Funzioni condivise per la suite SYS/Sepolia (Python 3.10+, web3.py 7.x).
Non firma né invia transazioni.
"""
from __future__ import annotations
import csv
import json
import os
import tempfile
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parent
CHAIN_ID = 11155111
LIFECYCLE_COLS = ["request_id", "phase", "start_time", "end_time", "elapsed_s", "completed", "tx_hash", "block_number", "seed_equal", "version_equal", "recovery_scenario"]
GAS_COLS = ["request_id", "operation", "tx_hash", "status", "gas_used", "effective_gas_price_wei", "body_count", "storage_case"]
DETAIL_COLS = ["attempt", "request_id", "request_tx_hash", "vrf_tx_hash", "claim_tx_hash", "token_id", "seed", "algorithm_version", "chain_seed_equal", "chain_version_equal", "unity_seed_equal", "unity_version_equal", "status", "error"]


def now() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="milliseconds")


def to_seconds(start: str, end: str) -> float:
    return (datetime.fromisoformat(end) - datetime.fromisoformat(start)).total_seconds()


def block_iso(seconds_since_epoch: int) -> str:
    return datetime.fromtimestamp(int(seconds_since_epoch), timezone.utc).isoformat(timespec="milliseconds")


def load_json(path: Path, default: Any = None) -> Any:
    if not path.is_file():
        return default
    with path.open(encoding="utf-8-sig") as f:
        return json.load(f)


def save_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    fd, name = tempfile.mkstemp(prefix=".sys_", suffix=".tmp", dir=path.parent)
    try:
        with os.fdopen(fd, "w", encoding="utf-8", newline="\n") as f:
            json.dump(value, f, indent=2, ensure_ascii=False)
            f.write("\n")
            f.flush()
            os.fsync(f.fileno())
        os.replace(name, path)
    finally:
        if os.path.exists(name):
            os.unlink(name)


def save_csv(path: Path, fieldnames: list[str], rows: list[dict]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    fd, name = tempfile.mkstemp(prefix=".sys_", suffix=".csv", dir=path.parent)
    try:
        with os.fdopen(fd, "w", encoding="utf-8", newline="") as f:
            w = csv.DictWriter(f, fieldnames=fieldnames, extrasaction="ignore")
            w.writeheader()
            w.writerows(rows)
            f.flush()
            os.fsync(f.fileno())
        os.replace(name, path)
    finally:
        if os.path.exists(name):
            os.unlink(name)


def read_csv(path: Path) -> list[dict]:
    if not path.is_file():
        return []
    with path.open(encoding="utf-8-sig", newline="") as f:
        return list(csv.DictReader(f))


def get_rpc_context(rpc_url: str, abi_file: Path, address_file: Path):
    try:
        from web3 import Web3
    except ImportError as exc:
        raise RuntimeError("Dipendenza assente: python -m pip install -r requirements.txt") from exc
    if not rpc_url.lower().startswith(("https://", "http://")):
        raise ValueError("SYS_RPC_URL deve usare HTTP(S)")
    address = address_file.read_text(encoding="utf-8-sig").strip()
    abi = load_json(abi_file)
    if not isinstance(abi, list):
        raise ValueError("ABI JSON deve essere una lista")
    w3 = Web3(Web3.HTTPProvider(rpc_url, request_kwargs={"timeout": 25}))
    if int(w3.eth.chain_id) != CHAIN_ID:
        raise RuntimeError(f"Chain ID inatteso: {w3.eth.chain_id}; atteso {CHAIN_ID}")
    address = Web3.to_checksum_address(address)
    if not w3.eth.get_code(address):
        raise RuntimeError("Contratto assente all'indirizzo specificato sulla rete Sepolia")
    contract = w3.eth.contract(address=address, abi=abi)
    expected_fns = ("requestSystemMint", "claimSystem", "mintPrice", "vrfRequests", "systems", "ownerOf", "balanceOf", "algorithmVersion")
    expected_events = ("SystemRequested", "RandomnessArrived", "SystemClaimed")
    for name in expected_fns:
        if not hasattr(contract.functions, name):
            raise RuntimeError(f"Funzione ABI mancante: {name}")
    for name in expected_events:
        if not hasattr(contract.events, name):
            raise RuntimeError(f"Evento ABI mancante: {name}")
    return w3, contract


def event_in_receipt(contract: Any, name: str, receipt: Any, **filters: Any) -> dict:
    from web3.logs import DISCARD
    matches = getattr(contract.events, name)().process_receipt(receipt, errors=DISCARD)
    for event in matches:
        if all(str(event["args"].get(k)).lower() == str(v).lower() for k, v in filters.items()):
            return dict(event["args"])
    raise RuntimeError(f"Evento {name} mancante nella ricevuta {receipt['transactionHash'].hex()}")


def get_event_range(contract: Any, name: str, start: int, stop: int, filters: dict, chunk: int = 400):
    event = getattr(contract.events, name)()
    out = []
    for lo in range(start, stop + 1, chunk):
        hi = min(lo + chunk - 1, stop)
        out.extend(event.get_logs(from_block=lo, to_block=hi, argument_filters=filters))
    return out


def ints_equal(*values: Any) -> bool:
    return len(set(int(v) for v in values)) == 1


def load_obs(path: Path, key: str) -> dict[str, dict]:
    """Indicizza CSV esterni, rifiutando chiavi duplicate o vuote."""
    result = {}
    for row in read_csv(path):
        if key not in row:
            raise ValueError(f"{path}: manca colonna {key}")
        k = str(row[key]).strip()
        if not k or k in result:
            raise ValueError(f"{path}: chiave mancante/duplicata {k!r}")
        result[k] = row
    return result
