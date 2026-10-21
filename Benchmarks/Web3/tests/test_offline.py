"""Verifica I/O del BLOCCO B SENZA RETE, wallet o dati di tesi.
Eseguire con python -m unittest discover -s tests -v
"""
import csv
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

class OfflineIOTests(unittest.TestCase):
    def test_gas_collector_group_and_plot(self):
        with tempfile.TemporaryDirectory() as d:
            folder = Path(d)
            cycles = []
            receipts = {}
            for i in range(12):
                seed = str(10**37 + i)
                rid = str(500 + i)
                txs = [f"0x{(3*i+j+1):064x}" for j in range(3)]
                cycles.append({"attempt": i + 1, "request_id": rid, "seed": seed,
                    "version": "1", "status": "complete", "request_storage_case": "request:pending0",
                    "claim_storage_case": "claim:balance_positive",
                    "request_tx_hash": txs[0], "vrf_tx_hash": txs[1], "claim_tx_hash": txs[2]})
                for j, h in enumerate(txs):
                    receipts[h] = {"status": 1, "gasUsed": 100000 + j*1000 + (i%6)*4,
                                   "effectiveGasPrice": 1000000000}
            state = {"chain_id":11155111,"contract":"0x123","wallet":"0x456","cycles":cycles}
            (folder/'web3_state.json').write_text(json.dumps(state), encoding='utf-8')
            (folder/'receipts.json').write_text(json.dumps(receipts), encoding='utf-8')
            with (folder/'seed_body_counts.csv').open('w',newline='') as f:
                w = csv.writer(f);w.writerow(['seed','body_count','algorithm_version'])
                for i in range(12):w.writerow([10**37+i,2+i,1])
            cmd = [sys.executable, str(ROOT/'gas_receipt_collector.py'),
                    '--state',str(folder/'web3_state.json'),'--out',str(folder),
                    '--body-counts',str(folder/'seed_body_counts.csv'),
                    '--offline-receipts',str(folder/'receipts.json')]
            p = subprocess.run(cmd, text=True, capture_output=True)
            self.assertEqual(p.returncode,0,p.stderr)
            with (folder/'gas_receipts.csv').open() as f:
                rows=list(csv.DictReader(f))
            self.assertEqual(len(rows),36)
            self.assertTrue(all(r['body_count'] for r in rows))
            with (folder/'gas_group_summary.csv').open() as f:
                delta=list(csv.DictReader(f))
            self.assertEqual(len(delta),3)
            # Grafico con un CSV mock strutturalmente conforme.
            with (folder/'web3_lifecycle.csv').open('w', newline='') as f:
                w=csv.writer(f)
                w.writerow(['request_id','phase','start_time','end_time','elapsed_s','completed','tx_hash','block_number','seed_equal','version_equal','recovery_scenario'])
                for i in range(12):
                    for ph in ['request','vrf','poll','claim','import']:
                        w.writerow([500+i,ph,'','','12.0','True','','','','',''])
            p = subprocess.run([sys.executable,str(ROOT/'plot_web3_stages.py'),
                    '--infile',str(folder/'web3_lifecycle.csv'),'--out',str(folder)],
                    capture_output=True,text=True)
            self.assertEqual(p.returncode,0,p.stderr)
            self.assertTrue((folder/'web3_stages.csv').is_file())
            self.assertTrue((folder/'web3_stage_latency.pdf').stat().st_size>1000)

if __name__ == '__main__':
    unittest.main()
