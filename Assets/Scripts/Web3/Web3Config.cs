using System.Text.RegularExpressions;
using UnityEngine;

public static class Web3Config
{
    public static string ContractAddress { get; private set; }
    public static string ContractABI { get; private set; }

    public static bool IsLoaded { get; private set; } = false;

    public static void LoadConfiguration()
    {
        if (IsLoaded)
            return;

        ContractAddress = null;
        ContractABI = null;
        IsLoaded = false;

        TextAsset addressFile =
            Resources.Load<TextAsset>("smart_contract_address");

        TextAsset abiFile =
            Resources.Load<TextAsset>("smart_contract_abi");

        if (addressFile == null)
        {
            Debug.LogError(
                "Configuration error: smart_contract_address.txt not found in Resources."
            );
            return;
        }

        if (abiFile == null)
        {
            Debug.LogError(
                "Configuration error: smart_contract_abi.json not found in Resources."
            );
            return;
        }

        string address = addressFile.text.Trim();
        string abi = abiFile.text.Trim();

        // Check if the contract address is in the correct format (0x followed by 40 hexadecimal characters).
        if (!Regex.IsMatch(address, @"\A0x[0-9a-fA-F]{40}\z"))
        {
            Debug.LogError(
                "Configuration error: contract address has an invalid format."
            );
            return;
        }

        // Check if the ABI is not empty and has the structure of a JSON array.
        if (string.IsNullOrWhiteSpace(abi) ||
            !abi.StartsWith("[") ||
            !abi.EndsWith("]"))
        {
            Debug.LogError(
                "Configuration error: contract ABI is empty or has an invalid basic structure."
            );
            return;
        }

        // Publish the configuration only after passing all checks.
        ContractAddress = address;
        ContractABI = abi;
        IsLoaded = true;

        Debug.Log("Web3 configuration loaded successfully.");
    }
}