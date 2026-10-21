#!/usr/bin/env python3
"""Genera web3_stages.csv e figura accademica vettoriale dalle fasi reali."""
import argparse
import statistics
from collections import defaultdict
from pathlib import Path
from sys_web3_common import ROOT, read_csv, save_csv

PHASES = [("request", "Request"), ("vrf", "Chainlink VRF"), ("poll", "Polling RPC"),
          ("claim", "Claim"), ("import", "Inventario")]

def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--infile", type=Path, default=ROOT / "output/web3_lifecycle.csv")
    p.add_argument("--out", type=Path, default=ROOT / "output")
    a = p.parse_args()
    rows = read_csv(a.infile)
    if not rows:
        raise ValueError(f"CSV vuoto/non trovato: {a.infile}")
    agg = defaultdict(list)
    for r in rows:
        if r["phase"] in dict(PHASES) and str(r["completed"]).lower() == "true" and r["elapsed_s"]:
            v = float(r["elapsed_s"])
            if v >= 0:
                agg[r["phase"]].append(v)
    metrics = []
    for name, label in PHASES:
        values = agg[name]
        if values:
            metrics.append(dict(phase=name, samples=len(values), mean_s=statistics.mean(values),
                std_s=statistics.stdev(values) if len(values) > 1 else 0.0,
                min_s=min(values), max_s=max(values)))
        else:
            metrics.append(dict(phase=name, samples=0, mean_s="", std_s="", min_s="", max_s=""))
    save_csv(a.out / "web3_stages.csv",
             ["phase", "samples", "mean_s", "std_s", "min_s", "max_s"], metrics)
    available = [m for m in metrics if m["samples"]]
    if not available:
        print("Nessun campione completo; CSV prodotto, grafico omesso.")
        return
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    plt.rcParams.update({"font.family": "serif", "mathtext.fontset": "cm", "font.size": 10,
                         "pdf.fonttype": 42, "axes.spines.top": False, "axes.spines.right": False})
    fig, ax = plt.subplots(figsize=(8.5, 4.1), layout="constrained")
    keys = [dict(PHASES)[m["phase"]] for m in available]
    vals = [float(m["mean_s"]) for m in available]
    sd = [float(m["std_s"]) for m in available]
    ax.barh(keys[::-1], vals[::-1], xerr=sd[::-1], color="#42678b", ecolor="#333333",
            capsize=3, label="Media e deviazione standard")
    ax.set_xlabel("Durata della fase [s]")
    ax.grid(axis="x", alpha=0.5, linestyle=":")
    ax.set_axisbelow(True)
    ax.legend(frameon=False, loc="lower right")
    fig.savefig(a.out / "web3_stage_latency.pdf", bbox_inches="tight")
    plt.close(fig)
    print("Creati:", a.out / "web3_stages.csv", "e", a.out / "web3_stage_latency.pdf")

if __name__ == "__main__":
    main()
