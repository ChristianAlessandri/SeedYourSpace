using System;
using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;

// Aggiungere a un GameObject della scena AUDIT, non nella scena Web3.
// Richiede gli hook di benchmarkLogicOnly già introdotti nel Blocco A.
public sealed class Web3SeedBodyCountExporter : MonoBehaviour
{
    [SerializeField] private StarSystemGenerator generator;
    [SerializeField] private bool runOnStart = true;

    private void Start() { if (runOnStart) StartCoroutine(Run()); }

    public IEnumerator Run()
    {
        if (generator == null || generator.enabled || generator.dioramaBuilder != null)
        {
            Debug.LogError("[SYS B] Occorre il generatore disabilitato e senza diorama.");
            yield break;
        }
        string dir = Path.Combine(Application.persistentDataPath, "SYSBenchmarks");
        string input = Path.Combine(dir, "onchain_seed_manifest.csv");
        if (!File.Exists(input))
        {
            Debug.LogError("[SYS B] Copiare onchain_seed_manifest.csv in " + dir);
            yield break;
        }
        string[] lines = File.ReadAllLines(input);
        if (lines.Length < 2 || lines[0].Trim() != "request_id,token_id,seed,algorithm_version")
        {
            Debug.LogError("[SYS B] Intestazione manifest non conforme.");
            yield break;
        }
        generator.benchmarkLogicOnly = true;
        string output = Path.Combine(dir, "seed_body_counts.csv");
        using (var csv = new SysBenchmarkCsv("seed_body_counts.csv", "seed", "body_count", "algorithm_version"))
        {
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                string[] cells = lines[i].Split(',');
                if (cells.Length != 4 || !int.TryParse(cells[3], NumberStyles.None,
                    CultureInfo.InvariantCulture, out int version))
                    throw new FormatException("Manifest errato alla riga " + (i + 1));
                // Seed decimale uint256: NON convertire in int/long.
                string seed = cells[2];
                generator.algorithmVersion = version;
                generator.GenerateCompleteStarSystem(seed);
                if (generator.SystemStar == null || generator.SystemPlanets == null)
                    throw new InvalidOperationException("Generazione nulla: " + seed);
                int count = 1 + generator.SystemPlanets.Count;
                foreach (PlanetData planet in generator.SystemPlanets) count += planet.moons.Count;
                csv.Write(seed, count, version);
                if ((i % 10) == 0) yield return null;
            }
            csv.Flush();
            output = csv.PathName;
        }
        Debug.Log("[SYS B] Generati body count: " + output);
    }
}
