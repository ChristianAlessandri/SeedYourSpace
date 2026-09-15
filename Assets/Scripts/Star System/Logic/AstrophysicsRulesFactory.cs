using UnityEngine;

public static class AstrophysicsRulesFactory
{
    public static IAstrophysicsRules CreateRules(int version)
    {
        switch (version)
        {
            case 1:
                return new AstrophysicsRulesV1();
            // Future versions can be added here as needed, for example:
            // case 2: return new AstrophysicsRulesV2();
            default:
                Debug.LogWarning($"[AstrophysicsRulesFactory] Version {version} not supported. Falling back to V1.");
                return new AstrophysicsRulesV1();
        }
    }
}