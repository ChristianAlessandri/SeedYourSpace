using System;
using System.IO;
using System.Numerics;
using System.Text;
using UnityEngine;

// Registra il valore realmente consumato dal generatore DOPO SystemDataBridge.
public static class Web3BridgeBenchmarkLog
{
    private static BigInteger pendingTokenId;
    private static BigInteger pendingSeed;
    private static BigInteger pendingVersion;
    private static bool hasPending;

    public static void Staged(BigInteger tokenId, BigInteger seed, BigInteger version)
    {
        pendingTokenId = tokenId;
        pendingSeed = seed;
        pendingVersion = version;
        hasPending = true;
    }

    public static void OnConsumed(string actualSeed, int actualVersion)
    {
        if (!hasPending) return;
        string dir = Path.Combine(Application.persistentDataPath, "SYSBenchmarks");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "unity_bridge_observations.csv");
        if (!File.Exists(path))
            File.WriteAllText(path, "token_id,seed,algorithm_version,utc,staged_equal\n", new UTF8Encoding(false));
        bool equal = actualSeed == pendingSeed.ToString() &&
                     new BigInteger(actualVersion) == pendingVersion;
        string line = pendingTokenId + "," + actualSeed + "," + actualVersion + "," +
            DateTime.UtcNow.ToString("o") + "," + (equal ? "true" : "false") + "\n";
        File.AppendAllText(path, line, new UTF8Encoding(false));
        hasPending = false;
    }
}
