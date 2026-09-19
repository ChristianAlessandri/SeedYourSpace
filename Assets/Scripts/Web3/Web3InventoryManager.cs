using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using UnityEngine;
using Nethereum.Web3;
using Nethereum.Contracts;
using Nethereum.ABI.FunctionEncoding.Attributes;

public class Web3InventoryManager : MonoBehaviour
{
    [Header("Contract Configuration")]
    public string contractAddress = "YOUR_CONTRACT_ADDRESS_HERE";
    [TextArea(2, 5)]
    public string contractABI = "PASTE_YOUR_LONG_ABI_HERE";

    [Header("UI Spawning")]
    public GameObject cardPrefab;
    public Transform scrollViewContent;

    [System.Serializable]
    public class SystemNFT
    {
        public BigInteger TokenId;
        public BigInteger Seed;
        public BigInteger AlgorithmVersion;
    }

    [FunctionOutput]
    public class SystemDataDTO : IFunctionOutputDTO
    {
        [Parameter("uint256", "seed", 1)]
        public BigInteger Seed { get; set; }

        [Parameter("uint256", "algorithmVersion", 2)]
        public BigInteger AlgorithmVersion { get; set; }
    }

    public List<SystemNFT> userInventory = new List<SystemNFT>();

    public async void LoadUserInventory()
    {
        if (Web3AuthManager.Web3Instance == null) return;

        var web3 = Web3AuthManager.Web3Instance;
        string userAddress = Web3AuthManager.CurrentAccount.Address;
        var contract = web3.Eth.GetContract(contractABI, contractAddress);

        try
        {
            var balanceOfFunction = contract.GetFunction("balanceOf");
            BigInteger balance = await balanceOfFunction.CallAsync<BigInteger>(userAddress);

            var tokenOfOwnerByIndexFunc = contract.GetFunction("tokenOfOwnerByIndex");
            var systemsMappingFunc = contract.GetFunction("systems");

            // Clean up old cards if we refresh the inventory
            foreach (Transform child in scrollViewContent)
            {
                Destroy(child.gameObject);
            }
            userInventory.Clear();

            for (int i = 0; i < balance; i++)
            {
                BigInteger tokenId = await tokenOfOwnerByIndexFunc.CallAsync<BigInteger>(userAddress, i);
                var systemData = await systemsMappingFunc.CallDeserializingToObjectAsync<SystemDataDTO>(tokenId);

                userInventory.Add(new SystemNFT { TokenId = tokenId, Seed = systemData.Seed, AlgorithmVersion = systemData.AlgorithmVersion });

                // Clone the Prefab and put it inside the ScrollView
                GameObject newCard = Instantiate(cardPrefab, scrollViewContent);
                newCard.GetComponent<InventoryUICard>().SetupCard(tokenId, systemData.Seed, systemData.AlgorithmVersion);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SYS: Failed to load inventory: {e.Message}");
        }
    }
}