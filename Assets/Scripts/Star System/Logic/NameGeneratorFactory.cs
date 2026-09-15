using UnityEngine;

public static class NameGeneratorFactory
{
    public static INameGenerator CreateNameGenerator(int version, string jsonContent)
    {
        switch (version)
        {
            case 1:
                return new MarkovNameGeneratorV1(jsonContent);
            // Future versions can be added here, e.g.:
            // case 2: return new NeuralNetworkNameGenerator(jsonContent);
            default:
                Debug.LogWarning($"[NameGeneratorFactory] Version {version} not supported. Falling back to V1.");
                return new MarkovNameGeneratorV1(jsonContent);
        }
    }
}