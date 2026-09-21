using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Numerics;
using Nethereum.Web3;
using Nethereum.Hex.HexTypes;

public class Web3MintManager : MonoBehaviour
{
    private readonly decimal mintPriceEth = 0.001m;

    [Header("Hub Interface")]
    public GameObject web3HubPanel;
    public Button discoverNewSystemButton;
    public TextMeshProUGUI statusText; 

    [Header("Confirmation Modal")]
    public GameObject confirmationModal;
    public TextMeshProUGUI feeBreakdownText;
    public Button confirmTransactionButton;
    public Button cancelTransactionButton;

    private object cachedTxInput; 

    private void Start()
    {
        // Ensure the modal is hidden when the Hub loads
        confirmationModal.SetActive(false);

        // Bind the Hub button to open the modal (after estimating gas)
        discoverNewSystemButton.onClick.AddListener(PrepareMintTransaction);
        
        // Bind the Modal buttons
        confirmTransactionButton.onClick.AddListener(ExecuteMintTransaction);
        cancelTransactionButton.onClick.AddListener(CancelTransaction);
    }

    private async void PrepareMintTransaction()
    {
        if (Web3AuthManager.Web3Instance == null || !Web3Config.IsLoaded) return;

        discoverNewSystemButton.interactable = false;
        statusText.text = "Estimating gas fees...";

        var web3 = Web3AuthManager.Web3Instance;
        string userAddress = Web3AuthManager.CurrentAccount.Address;
        
        var contract = web3.Eth.GetContract(Web3Config.ContractABI, Web3Config.ContractAddress);
        var requestFunction = contract.GetFunction("requestSystemMint");

        try
        {
            BigInteger valueInWei = Web3.Convert.ToWei(mintPriceEth);
            
            // BYPASS Nethereum overloads: Get the raw ABI-encoded data for 0 parameters
            string txData = requestFunction.GetData();

            //  Build the TransactionInput manually
            var txInput = new Nethereum.RPC.Eth.DTOs.TransactionInput()
            {
                To = Web3Config.ContractAddress,
                From = userAddress,
                Data = txData,
                Value = new HexBigInteger(valueInWei)
            };

            // Ask the network to estimate the gas using the raw transaction
            HexBigInteger estimatedGas = await web3.Eth.Transactions.EstimateGas.SendRequestAsync(txInput);
            HexBigInteger currentGasPrice = await web3.Eth.GasPrice.SendRequestAsync();

            txInput.Gas = estimatedGas;
            txInput.GasPrice = currentGasPrice;

            BigInteger totalGasFeeWei = estimatedGas.Value * currentGasPrice.Value;
            decimal totalGasFeeEth = Web3.Convert.FromWei(totalGasFeeWei);
            decimal totalCostEth = mintPriceEth + totalGasFeeEth;

            feeBreakdownText.text = 
                $"NFT Cost: {mintPriceEth} ETH\n" +
                $"Est. Gas Fee: {totalGasFeeEth:0.00000} ETH\n" +
                $"------------------\n" +
                $"Total Max Cost: {totalCostEth:0.00000} ETH";

            cachedTxInput = txInput;
            statusText.text = "";
            
            web3HubPanel.SetActive(false);
            confirmationModal.SetActive(true);
        }
        catch (System.Exception e)
        {
            statusText.text = "Error estimating gas. See console.";
            Debug.LogError($"SYS Gas Estimation Failed: {e.Message}");
            discoverNewSystemButton.interactable = true;
        }
    }

    private async void ExecuteMintTransaction()
    {
        confirmationModal.SetActive(false);
        statusText.text = "Sending transaction to Sepolia...";

        var web3 = Web3AuthManager.Web3Instance;

        try
        {
            var txInput = (Nethereum.RPC.Eth.DTOs.TransactionInput)cachedTxInput;

            // Broadcast the manually created transaction via the TransactionManager
            string txHash = await web3.Eth.TransactionManager.SendTransactionAsync(txInput);
            
            statusText.text = "Transaction sent! Waiting for Chainlink VRF...\nTx Hash: " + txHash;
            Debug.Log($"SYS: Mint Request sent! Hash: {txHash}");

            // TODO: Next step is polling for the VRF fulfillment here
        }
        catch (System.Exception e)
        {
            statusText.text = "Transaction failed or rejected.";
            Debug.LogError($"SYS Tx Error: {e.Message}");
            discoverNewSystemButton.interactable = true;
        }
    }

    private void CancelTransaction()
    {
        cachedTxInput = null;
        confirmationModal.SetActive(false);
        web3HubPanel.SetActive(true);
        discoverNewSystemButton.interactable = true;
        statusText.text = "Transaction cancelled.";
    }
}