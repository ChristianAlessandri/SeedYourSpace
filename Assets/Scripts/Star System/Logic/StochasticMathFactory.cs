using UnityEngine;

public static class StochasticMathFactory
{
    public static IStochasticMath CreateMath(int version)
    {
        switch (version)
        {
            case 1:
                return new StochasticMathV1();
            // Future versions can be added here, e.g.:
            // case 2: return new StochasticMathV2();
            default:
                Debug.LogWarning($"[StochasticMathFactory] Version {version} not supported. Falling back to V1.");
                return new StochasticMathV1();
        }
    }
}