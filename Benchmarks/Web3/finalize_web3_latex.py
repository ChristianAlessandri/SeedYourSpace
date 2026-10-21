#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""SYS -- consuntivo Sepolia senza operazioni RPC né transazioni.

Utilizzo dalla directory SYS_BlockB_Sepolia:
    python finalize_web3_latex.py --out output --rescued 8,20

Input:
    output/web3_lifecycle.csv
    output/web3_details.csv
    output/web3_state.json (opzionale, usato SOLO per i flag di recovery)
    output/web3_recovery_attempts.csv (opzionale)
    output/gas_stats.csv
    output/gas_group_summary.csv
    output/gas_receipts.csv

Output (in output/latex):
    tabella_4_1_web3.tex, tabella_4_2_gas.tex,
    web3_stage_latency_nominal.pdf, web3_stages_nominal.csv,
    report_qualita_web3.txt

Solo lettura degli input; non importa web3.py, non legge .env e non accede alla rete.
Media e deviazione standard campionaria. I successi includono i cicli
recuperati, ma i tempi del claim ed end-to-end recuperati manualmente
non contribuiscono alle statistiche delle latenze nominali.
"""
from __future__ import annotations
import argparse
import csv
import json
import math
import statistics
from collections import Counter, defaultdict
from pathlib import Path

PHASES = {
    "request": ("Conferma \\texttt{requestSystemMint}", "WEB3_REQUEST"),
    "vrf": ("Adempimento \\texttt{fulfillRandomWords}", "WEB3_VRF"),
    "poll": ("Rilevamento della VRF via RPC", "WEB3_POLL"),
    "claim": ("Conferma \\texttt{claimSystem}", "WEB3_CLAIM"),
    "import": ("Letture RPC per inventario", "WEB3_IMPORT"),
    "e2e": ("Ciclo completo, esclusa attesa utente", "WEB3_E2E"),
    "recovery": ("Ripresa dopo nuovo accesso RPC", "WEB3_RECOVERY"),
}
NOMINAL_EXCLUDE = {"claim", "e2e"}
GRAPH_PHASES = [
    ("request", "Request"), ("vrf", "Chainlink VRF"),
    ("poll", "Polling RPC"), ("claim", "Claim"), ("import", "Inventario"),
]
GAS = [("request", r"\texttt{requestSystemMint}"),
       ("vrf", r"Callback \texttt{fulfillRandomWords}"),
       ("claim", r"\texttt{claimSystem}")]


def read_csv(path: Path, required=True):
    if not path.is_file():
        if required:
            raise FileNotFoundError(f"Input mancante: {path}")
        return []
    with path.open(encoding="utf-8-sig", newline="") as f:
        return list(csv.DictReader(f))


def number(x):
    try:
        x = float(str(x).strip())
        return x if math.isfinite(x) else None
    except (ValueError, TypeError):
        return None


def yes(x):
    return str(x).strip().lower() in {"true", "1", "yes"}


def numeric_str(x, digits=2, integer=False):
    if x is None or not math.isfinite(float(x)):
        return r"\textemdash"
    if integer:
        return f"{round(float(x)):d}"
    return f"{float(x):.{digits}f}".replace(".", "{,}")


def cell_stats(nums, precision=2):
    if not nums:
        return r"\textemdash", r"\textemdash"
    mean = statistics.mean(nums)
    sd = statistics.stdev(nums) if len(nums) > 1 else None
    first = rf"${numeric_str(mean, precision)} \pm {numeric_str(sd, precision)}$" if sd is not None else rf"${numeric_str(mean, precision)}$"
    second = rf"${numeric_str(min(nums),precision)}$--${numeric_str(max(nums),precision)}$"
    return first, second


def pct(count, total):
    return rf"${numeric_str(100 * count / total, 1)}$" if total else r"\textemdash"


def cli():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--out", type=Path, default=Path("output"), help="Directory CSV già prodotti dal batch")
    p.add_argument("--dest", type=Path, help="Directory file LaTeX e PDF (default: OUT/latex)")
    p.add_argument("--rescued", type=str, default="", help="Tentativi di claim recuperati manualmente, es.: 8,20")
    p.add_argument("--cycles", type=int, default=30)
    p.add_argument("--recoveries", type=int, default=10)
    return p.parse_args()


def main():
    a = cli()
    dest = a.dest or a.out / "latex"
    dest.mkdir(parents=True, exist_ok=True)
    details = read_csv(a.out / "web3_details.csv")
    lifecycle = read_csv(a.out / "web3_lifecycle.csv")
    gas_stats = read_csv(a.out / "gas_stats.csv")
    gas_rows = read_csv(a.out / "gas_receipts.csv")
    delta_rows = read_csv(a.out / "gas_group_summary.csv", required=False)
    recovery_attempts = read_csv(a.out / "web3_recovery_attempts.csv", required=False)
    state_file = a.out / "web3_state.json"
    state = json.loads(state_file.read_text(encoding="utf-8-sig")) if state_file.is_file() else {}

    if len(details) != a.cycles:
        raise ValueError(f"web3_details.csv: {len(details)} cicli, attesi {a.cycles}; dati non pronti")
    attempts = {str(r["request_id"]): int(r["attempt"]) for r in details if r.get("request_id")}
    manual = {int(s) for s in a.rescued.split(",") if s.strip()}
    for c in state.get("cycles", []):
        if c.get("claim_manual_recovery") or c.get("exclude_from_nominal_latency"):
            manual.add(int(c["attempt"]))
    if any(i < 1 or i > a.cycles for i in manual):
        raise ValueError(f"Indici tentativo di recupero non validi: {sorted(manual)}")

    n_complete = sum(str(x.get("status", "")).lower() == "complete" for x in details)
    if n_complete != a.cycles:
        raise ValueError(f"Cicli completi: {n_complete}/{a.cycles}. Controllare il batch prima di compilare la tesi.")
    # Mappatura unica request_id -> phase. Eventuali duplicati sono un errore sperimentale.
    phase_map = {}
    for r in lifecycle:
        key = (str(r.get("request_id", "")), str(r.get("phase", "")).lower())
        if key in phase_map:
            raise ValueError(f"Doppia fase in web3_lifecycle.csv: {key}")
        phase_map[key] = r
    stagevalues = {}
    numerators = {}
    for phase in PHASES:
        eligible = [r for r in details if (int(r["attempt"]) <= a.recoveries if phase == "recovery" else True)]
        values = []
        completed = 0
        for dr in eligible:
            row = phase_map.get((str(dr["request_id"]), phase))
            if not row or not yes(row.get("completed", "")):
                continue
            completed += 1
            n = number(row.get("elapsed_s"))
            if n is not None and n >= 0 and not (phase in NOMINAL_EXCLUDE and int(dr["attempt"]) in manual):
                values.append(n)
        stagevalues[phase] = values
        numerators[phase] = (completed, len(eligible))

    # Confronto Unity esplicito: non confondere roundtrip on-chain con middleware.
    bridge_observed = [r for r in details if str(r.get("unity_seed_equal", "")).strip() != ""
                       and str(r.get("unity_version_equal", "")).strip() != ""]
    bridge_matches = sum(yes(r["unity_seed_equal"]) and yes(r["unity_version_equal"]) for r in bridge_observed)
    chain_ok = sum(yes(r.get("chain_seed_equal")) and yes(r.get("chain_version_equal")) for r in details)

    rows1 = []
    for phase in ("request", "vrf", "poll", "claim", "import", "e2e"):
        label, _ = PHASES[phase]
        vals = stagevalues[phase]
        mean_sd, extremes = cell_stats(vals)
        succeeded, denominator = numerators[phase]
        rows1.append(f"{label} & {mean_sd} & {extremes} & {pct(succeeded, denominator)} \\\\")
    rows1.append(r"\midrule")
    if bridge_observed:
        percent_bridge = pct(bridge_matches, len(bridge_observed))
    else:
        percent_bridge = r"\textemdash"
    rows1.append(r"Integrità seed/versione nel bridge Unity & --- & --- & " + percent_bridge + r" \\")
    mean_sd, extremes = cell_stats(stagevalues["recovery"])
    succeeded, denom = numerators["recovery"]
    rows1.append(r"Ripresa dopo nuovo accesso RPC & " + mean_sd + " & " + extremes + " & " + pct(succeeded, denom) + r" \\")
    tex1 = "\n".join([
        r"\begin{table}[htbp]", r"\centering", r"\small", r"\setlength{\tabcolsep}{3pt}",
        r"\begin{tabular}{p{0.34\linewidth}p{0.25\linewidth}p{0.21\linewidth}p{0.12\linewidth}}",
        r"\toprule", r"\textbf{Fase o controllo} & \textbf{Media $\pm$ dev. st. [s]} & \textbf{Min--max [s]} & \textbf{Successo [\%]} \\",
        r"\midrule", *rows1, r"\bottomrule", r"\end{tabular}",
        r"\caption{Latenze e riuscita del ciclo Request--Fulfill--Claim su Sepolia. Le durate nominali di claim e ciclo completo escludono i tentativi con recupero manuale del claim; il successo considera comunque tutti i cicli. I tempi di recupero riguardano le riconnessioni RPC programmate. La concordanza del bridge Unity è calcolata sui soli token osservati dal client. La fase di import misura le letture RPC del token, non il tempo della UI Unity.}",
        r"\label{tab:eval-web3-latenze}", r"\end{table}", ""
    ])
    (dest / "tabella_4_1_web3.tex").write_text(tex1, encoding="utf-8")

    # Gas: la fonte primaria è la receipt; i valori statistici già esportati
    # dal collettore sono ricontrollati con il numero effettivo di righe valide.
    gas_by_op = defaultdict(list)
    for row in gas_rows:
        gas_by_op[row.get("operation", "")].append(row)
    aggregated = {r["operation"]: r for r in gas_stats}
    delta_by_op = defaultdict(list)
    for r in delta_rows:
        n = number(r.get("abs_delta_gas"))
        if n is not None:
            delta_by_op[r.get("operation", "")].append((r, n))
    gastex_rows = []
    details_delta = []
    for op, label in GAS:
        source = aggregated.get(op)
        if source is None:
            raise ValueError(f"gas_stats.csv: operazione mancante: {op}")
        good = [r for r in gas_by_op[op] if str(r.get("status", "")).strip() == "1"]
        if len(good) != int(source["successful"]):
            raise ValueError(f"gas_stats e gas_receipts non concordano per {op}")
        if len(good) != a.cycles:
            raise ValueError(f"Ricevute valide {op}: {len(good)}/{a.cycles}; analisi gas incompleta")
        gas_samples = [number(r.get("gas_used")) for r in good]
        if any(v is None for v in gas_samples):
            raise ValueError(f"gasUsed non numerico per {op}")
        mean_sd, extremes = cell_stats(gas_samples, precision=0)
        candidates = sorted(delta_by_op[op], key=lambda pair: -int(pair[0]["low_samples"]) - int(pair[0]["high_samples"]))
        if candidates:
            r, delta = candidates[0]
            delta_cell = rf"${numeric_str(delta, 0)}$"
            details_delta.append(f"{op}: caso {r['storage_case']}, gruppi {r['low_samples']}/{r['high_samples']}, delta {delta:.2f} gas")
        else:
            delta_cell = r"\textemdash"
            details_delta.append(f"{op}: delta non determinabile; servono seed_body_counts e gruppi omogenei")
        gastex_rows.append(f"{label} & {mean_sd} & {extremes} & {delta_cell} \\\\")
    tex2 = "\n".join([
        r"\begin{table}[htbp]", r"\centering", r"\small", r"\setlength{\tabcolsep}{3pt}",
        r"\begin{tabular}{p{0.31\linewidth}p{0.26\linewidth}p{0.21\linewidth}p{0.14\linewidth}}",
        r"\toprule", r"\textbf{Operazione} & \textbf{Media $\pm$ dev. st. [gas]} & \textbf{Min--max [gas]} & \textbf{Scarto gruppi [gas]} \\",
        r"\midrule", *gastex_rows, r"\bottomrule", r"\end{tabular}",
        r"\caption{Gas consumato dalle transazioni del ciclo di coniazione su Sepolia. Lo scarto confronta gruppi con diversa numerosità di corpi a parità di percorso di storage, quando i dati consentono il confronto; il trattino indica un confronto non determinabile. Il gas della callback VRF non coincide con l'addebito della sottoscrizione Chainlink.}",
        r"\label{tab:eval-gas}", r"\end{table}", ""
    ])
    (dest / "tabella_4_2_gas.tex").write_text(tex2, encoding="utf-8")

    # Grafico usa gli STESSI valori nominali di Tab. 4.1.
    with (dest / "web3_stages_nominal.csv").open("w", encoding="utf-8", newline="") as f:
        writer = csv.writer(f)
        writer.writerow(["phase", "samples", "mean_s", "std_s", "min_s", "max_s"])
        for op, label in GRAPH_PHASES:
            vals = stagevalues[op]
            writer.writerow([op, len(vals), statistics.mean(vals) if vals else "",
                             statistics.stdev(vals) if len(vals) > 1 else "",
                             min(vals) if vals else "", max(vals) if vals else ""])
    try:
        import matplotlib
        matplotlib.use("Agg")
        import matplotlib.pyplot as plt
    except ImportError:
        print("[AVVISO] matplotlib mancante: grafico non creato (pip install matplotlib)")
    else:
        plt.rcParams.update({"font.family": "serif", "mathtext.fontset": "cm", "font.size": 10,
                             "pdf.fonttype": 42, "axes.spines.top": False, "axes.spines.right": False})
        active = [(label, stagevalues[op]) for op, label in GRAPH_PHASES if stagevalues[op]]
        if active:
            labels = [t[0] for t in active]
            means = [statistics.mean(t[1]) for t in active]
            sds = [statistics.stdev(t[1]) if len(t[1]) > 1 else 0 for t in active]
            fig, ax = plt.subplots(figsize=(8.5, 4.15), layout="constrained")
            ax.barh(labels[::-1], means[::-1], xerr=sds[::-1], capsize=3,
                    color="#42678b", ecolor="#333333", label="Media e deviazione standard")
            ax.set_xlabel("Durata della fase [s]")
            ax.grid(axis="x", alpha=0.5, linestyle=":")
            ax.set_axisbelow(True)
            ax.legend(frameon=False, loc="lower right")
            fig.savefig(dest / "web3_stage_latency_nominal.pdf", bbox_inches="tight")
            plt.close(fig)

    missing = sorted(set(range(1, a.cycles+1)) - {int(x["attempt"]) for x in details})
    first_recovery_count = sum(yes(r.get("completed")) for r in recovery_attempts if str(r.get("attempt_number")) == "1")
    recovery_failed_attempts = sum(not yes(r.get("completed")) for r in recovery_attempts)
    report = [
        "CONTROLLO QUALITA' DEL BATCH SYS SEPOLIA", "",
        f"Cicli completi: {n_complete}/{a.cycles}",
        f"Tentativi mancanti: {missing}",
        f"Recuperi manuali del claim (esclusi solo da claim/E2E nominale): {sorted(manual)}",
        f"Riconnessioni RPC programmate: {numerators['recovery'][0]}/{numerators['recovery'][1]} riuscite",
        f"Tentativi di riconnessione registrati: {len(recovery_attempts)}; falliti: {recovery_failed_attempts}; primi tentativi riusciti: {first_recovery_count}",
        f"Roundtrip on-chain seed/versione: {chain_ok}/{len(details)}",
        f"Bridge Unity seed/versione: {bridge_matches}/{len(bridge_observed)} osservati (su {a.cycles} token)",
        "La voce 'Integrità seed/versione nel bridge Unity' in tabella non equivale all'integrità on-chain.",
        "", "Fasi (campioni temporali utilizzati; esiti riusciti / eleggibili):"
    ]
    for op in PHASES:
        good, den = numerators[op]
        report.append(f"  {op}: {len(stagevalues[op])} tempi; successi {good}/{den}")
    report += ["", "Confronto gas fra gruppi:", *["  " + s for s in details_delta]]
    report += ["", "NOTE METODOLOGICHE:",
               "- Il completamento del batch non rende nominali i cicli con recovery manuale.",
               "- I recuperi di claim dei cicli 8 e 20 NON sono le 10 riconnessioni RPC programmate.",
               "- Valutare eventuali altri intervalli di inattivita'/errori prima di assumere che tutte le altre durate siano nominali.",
               "- Misure di polling con timestamp catena/client sono sensibili alla sincronizzazione degli orologi."]
    (dest / "report_qualita_web3.txt").write_text("\n".join(report) + "\n", encoding="utf-8")
    print("\n".join(report))
    print("\nFile generati in", dest.resolve())


if __name__ == "__main__":
    main()
