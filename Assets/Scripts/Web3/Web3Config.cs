using UnityEngine;

public static class Web3Config
{
    public static string ContractAddress { get; private set; }
    public static string ContractABI { get; private set; }
    public static bool IsLoaded { get; private set; } = false;

    public static void LoadConfiguration()
    {
        if (IsLoaded) return;

        TextAsset addressFile = Resources.Load<TextAsset>("smart_contract_address");
        TextAsset abiFile = Resources.Load<TextAsset>("smart_contract_abi");

        if (addressFile != null)
        {
            ContractAddress = addressFile.text.Trim();
        }
        else
        {
            Debug.LogError("Error: smart_contract_address.txt not found in Resources!");
        }

        if (abiFile != null)
        {
            ContractABI = abiFile.text.Trim();
        }
        else
        {
            Debug.LogError("Error: smart_contract_abi.json not found in Resources!");
        }

        IsLoaded = true;
    }
}