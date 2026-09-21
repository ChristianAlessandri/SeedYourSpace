using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Web3;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

public class Web3MintManager : MonoBehaviour
{
    private readonly decimal mintPriceEth = 0.001m;

    [Header("Hub Interface")]
    public GameObject web3HubPanel;
    public Button discoverNewSystemButton;
    public Button claimSystemButton;
    public TextMeshProUGUI statusText; 

    [Header("Confirmation Modal")]
    public GameObject confirmationModal;
    public TextMeshProUGUI feeBreakdownText;
    public Button confirmTransactionButton;
    public Button cancelTransactionButton;

    private object cachedTxInput; 
    private BigInteger activeRequestId;

    private enum TransactionType { None, Mint, Claim }
    private TransactionType pendingAction = TransactionType.None;

    // --- DATA TRANSFER OBJECTS (DTOs) ---

    [Event("SystemRequested")]
    public class SystemRequestedEventDTO : IEventDTO
    {
        [Parameter("uint256", "requestId", 1, true)]
        public BigInteger RequestId { get; set; }
        [Parameter("address", "requester", 2, true)]
        public string Requester { get; set; }
        [Parameter("uint256", "algorithmVersion", 3, false)]
        public BigInteger AlgorithmVersion { get; set; }
    }

    [FunctionOutput]
    public class PendingRequestDTO : IFunctionOutputDTO
    {
        [Parameter("address", "minter", 1)]
        public string Minter { get; set; }
        [Parameter("uint256", "algorithmVersionSnapshot", 2)]
        public BigInteger AlgorithmVersionSnapshot { get; set; }
        [Parameter("uint256", "generatedSeed", 3)]
        public BigInteger GeneratedSeed { get; set; }
        [Parameter("bool", "isFulfilled", 4)]
        public bool IsFulfilled { get; set; }
    }

    // --- INITIALIZATION ---

    private void Start()
    {
        confirmationModal.SetActive(false);
        claimSystemButton.gameObject.SetActive(false);

        discoverNewSystemButton.onClick.AddListener(PrepareMintTransaction);
        claimSystemButton.onClick.AddListener(PrepareClaimTransaction);
        
        confirmTransactionButton.onClick.AddListener(ExecutePendingTransaction);
        cancelTransactionButton.onClick.AddListener(CancelTransaction);
    }

    // --- PHASE 1: MINTING ---

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
            string txData = requestFunction.GetData();

            var txInput = new Nethereum.RPC.Eth.DTOs.TransactionInput()
            {
                To = Web3Config.ContractAddress,
                From = userAddress,
                Data = txData,
                Value = new HexBigInteger(valueInWei)
            };

            HexBigInteger estimatedGas = await web3.Eth.Transactions.EstimateGas.SendRequestAsync(txInput);
            HexBigInteger currentGasPrice = await web3.Eth.GasPrice.SendRequestAsync();

            BigInteger safeGasLimit = (estimatedGas.Value * 120) / 100;
            BigInteger safeGasPrice = (currentGasPrice.Value * 115) / 100;

            txInput.Gas = new HexBigInteger(safeGasLimit);
            txInput.GasPrice = new HexBigInteger(safeGasPrice);

            BigInteger totalGasFeeWei = safeGasLimit * safeGasPrice;
            decimal totalGasFeeEth = Web3.Convert.FromWei(totalGasFeeWei);
            decimal totalCostEth = mintPriceEth + totalGasFeeEth;

            feeBreakdownText.text = 
                $"NFT Cost: {mintPriceEth} ETH\n" +
                $"Est. Gas Fee: {totalGasFeeEth:0.00000} ETH\n" +
                $"------------------\n" +
                $"Total Max Cost: {totalCostEth:0.00000} ETH";

            cachedTxInput = txInput;
            pendingAction = TransactionType.Mint;
            
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

    // --- PHASE 2: CLAIMING ---

    private async void PrepareClaimTransaction()
    {
        if (Web3AuthManager.Web3Instance == null || !Web3Config.IsLoaded) return;

        claimSystemButton.interactable = false;
        statusText.text = "Estimating claim gas fees...";

        var web3 = Web3AuthManager.Web3Instance;
        string userAddress = Web3AuthManager.CurrentAccount.Address;
        
        var contract = web3.Eth.GetContract(Web3Config.ContractABI, Web3Config.ContractAddress);
        var claimFunction = contract.GetFunction("claimSystem");

        try
        {
            string txData = claimFunction.GetData(activeRequestId);

            var txInput = new Nethereum.RPC.Eth.DTOs.TransactionInput()
            {
                To = Web3Config.ContractAddress,
                From = userAddress,
                Data = txData
            };

            HexBigInteger estimatedGas = await web3.Eth.Transactions.EstimateGas.SendRequestAsync(txInput);
            HexBigInteger currentGasPrice = await web3.Eth.GasPrice.SendRequestAsync();

            BigInteger safeGasLimit = (estimatedGas.Value * 120) / 100;
            BigInteger safeGasPrice = (currentGasPrice.Value * 115) / 100;

            txInput.Gas = new HexBigInteger(safeGasLimit);
            txInput.GasPrice = new HexBigInteger(safeGasPrice);

            BigInteger totalGasFeeWei = safeGasLimit * safeGasPrice;
            decimal totalGasFeeEth = Web3.Convert.FromWei(totalGasFeeWei);

            feeBreakdownText.text = 
                $"NFT Cost: 0.000 ETH (Already Paid)\n" +
                $"Est. Gas Fee: {totalGasFeeEth:0.00000} ETH\n" +
                $"------------------\n" +
                $"Total Max Cost: {totalGasFeeEth:0.00000} ETH";

            cachedTxInput = txInput;
            pendingAction = TransactionType.Claim;
            
            statusText.text = "";
            web3HubPanel.SetActive(false);
            confirmationModal.SetActive(true);
        }
        catch (System.Exception e)
        {
            statusText.text = "Error estimating claim gas.";
            Debug.LogError($"SYS Claim Gas Estimation Failed: {e.Message}");
            claimSystemButton.interactable = true;
        }
    }

    // --- TRANSACTION ROUTER ---

    private void ExecutePendingTransaction()
    {
        confirmationModal.SetActive(false);
        web3HubPanel.SetActive(true); // Return to hub to see status

        if (pendingAction == TransactionType.Mint)
        {
            ExecuteMintTransaction();
        }
        else if (pendingAction == TransactionType.Claim)
        {
            ExecuteFinalClaimTransaction();
        }
    }

    private void CancelTransaction()
    {
        cachedTxInput = null;
        pendingAction = TransactionType.None;
        
        confirmationModal.SetActive(false);
        web3HubPanel.SetActive(true);
        
        discoverNewSystemButton.interactable = true;
        claimSystemButton.interactable = true;
        statusText.text = "Transaction cancelled.";
    }

    // --- EXECUTION & TRACKING ---

    private async void ExecuteMintTransaction()
    {
        statusText.text = "Sending Request to Sepolia...";
        var web3 = Web3AuthManager.Web3Instance;

        try
        {
            var txInput = (Nethereum.RPC.Eth.DTOs.TransactionInput)cachedTxInput;
            string txHash = await web3.Eth.TransactionManager.SendTransactionAsync(txInput);
            
            Debug.Log($"SYS: Mint Request sent! Hash: {txHash}");
            MonitorMintingProcess(txHash);
        }
        catch (System.Exception e)
        {
            statusText.text = "Transaction failed or rejected.";
            discoverNewSystemButton.interactable = true;
            Debug.LogError($"SYS Mint Error: {e.Message}"); 
        }
    }

    private async void ExecuteFinalClaimTransaction()
    {
        statusText.text = "Materializing System (Claiming NFT)...";
        var web3 = Web3AuthManager.Web3Instance;

        try
        {
            var txInput = (Nethereum.RPC.Eth.DTOs.TransactionInput)cachedTxInput;
            string txHash = await web3.Eth.TransactionManager.SendTransactionAsync(txInput);
            
            Debug.Log($"SYS: Claim Hash: {txHash}");
            statusText.text = "Claim sent! Waiting for block confirmation...";

            Nethereum.RPC.Eth.DTOs.TransactionReceipt receipt = null;
            while (receipt == null)
            {
                await Task.Delay(3000);
                receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);
            }

            statusText.text = "Claim successful! Refreshing inventory...";

            claimSystemButton.gameObject.SetActive(false);
            discoverNewSystemButton.interactable = true;
            
            FindFirstObjectByType<Web3InventoryManager>().LoadUserInventory();

            await Task.Delay(3000);
            if (statusText.text == "Claim successful! Refreshing inventory...")
            {
                statusText.text = "";
            }
        }
        catch (System.Exception e)
        {
            statusText.text = "Claim failed. Try again.";
            claimSystemButton.interactable = true;
            Debug.LogError($"SYS Claim Error: {e.Message}");
        }
    }

    private async void MonitorMintingProcess(string txHash)
    {
        var web3 = Web3AuthManager.Web3Instance;
        var contract = web3.Eth.GetContract(Web3Config.ContractABI, Web3Config.ContractAddress);

        statusText.text = "Transmitting coordinates... (Waiting for block confirmation)";

        try
        {
            Nethereum.RPC.Eth.DTOs.TransactionReceipt receipt = null;
            while (receipt == null)
            {
                await Task.Delay(4000);
                receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);
            }

            var eventHandler = web3.Eth.GetEvent<SystemRequestedEventDTO>(Web3Config.ContractAddress);
            var decodedEvents = eventHandler.DecodeAllEventsForEvent(receipt.Logs);

            if (decodedEvents.Count == 0)
            {
                statusText.text = "Error: Could not retrieve Request ID.";
                discoverNewSystemButton.interactable = true;
                return;
            }

            activeRequestId = decodedEvents[0].Event.RequestId;
            
            var vrfRequestsFunc = contract.GetFunction("vrfRequests");
            bool isFulfilled = false;
            int attemptCounter = 0;

            while (!isFulfilled)
            {
                attemptCounter++;
                statusText.text = $"Link established (ID: {activeRequestId}).\nWaiting for Chainlink VRF... (Check {attemptCounter})";
                
                // Chainlink takes an average of 3–5 blocks (40–60 seconds).
                // We check every 12 seconds to avoid being blocked by the RPC node.
                await Task.Delay(12000); 

                try
                {
                    var reqData = await vrfRequestsFunc.CallDeserializingToObjectAsync<PendingRequestDTO>(activeRequestId);
                    if (reqData.IsFulfilled)
                    {
                        isFulfilled = true;
                    }
                }
                catch (System.Exception reqEx)
                {
                    Debug.LogWarning($"SYS RPC Polling timeout (Attempt {attemptCounter}): {reqEx.Message}");
                }
            }

            statusText.text = "Entropy received! Space coordinates calculated.";
            claimSystemButton.gameObject.SetActive(true);
        }
        catch (System.Exception e)
        {
            statusText.text = "Connection lost during monitoring.";
            discoverNewSystemButton.interactable = true;
            Debug.LogError($"SYS Monitor Error: {e.Message}");
        }
    }

    // --- RECOVERY SYSTEM (EDGE CASES) ---

    public async void CheckForPendingRequests()
    {
        if (Web3AuthManager.Web3Instance == null || !Web3Config.IsLoaded) return;

        var web3 = Web3AuthManager.Web3Instance;
        string userAddress = Web3AuthManager.CurrentAccount.Address;
        var contract = web3.Eth.GetContract(Web3Config.ContractABI, Web3Config.ContractAddress);

        try
        {
            statusText.text = "Checking blockchain for pending systems...";
            discoverNewSystemButton.interactable = false;

            // Get the current block number to avoid RPC limits
            var currentBlock = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            
            // Look back 40,000 blocks (roughly 5-6 days on Sepolia) to stay under the 50k limit
            BigInteger fromBlockValue = currentBlock.Value - 40000;
            if (fromBlockValue < 0) fromBlockValue = 0;
            var fromBlock = new Nethereum.Hex.HexTypes.HexBigInteger(fromBlockValue);

            // Fetch historical SystemRequested events within the safe range
            var eventHandler = web3.Eth.GetEvent<SystemRequestedEventDTO>(Web3Config.ContractAddress);
            var filter = eventHandler.CreateFilterInput(
                new Nethereum.RPC.Eth.DTOs.BlockParameter(fromBlock), 
                Nethereum.RPC.Eth.DTOs.BlockParameter.CreateLatest()
            );
            
            var events = await eventHandler.GetAllChangesAsync(filter);
            BigInteger? lastFoundRequestId = null;

            // Find the most recent request for this specific user
            foreach (var ev in events)
            {
                if (ev.Event.Requester.ToLower() == userAddress.ToLower())
                {
                    lastFoundRequestId = ev.Event.RequestId;
                }
            }

            if (lastFoundRequestId.HasValue)
            {
                // Check the current status of this request in the contract
                var vrfRequestsFunc = contract.GetFunction("vrfRequests");
                var reqData = await vrfRequestsFunc.CallDeserializingToObjectAsync<PendingRequestDTO>(lastFoundRequestId.Value);

                // If Minter is not empty (0x0...), the request is still active and unclaimed
                if (!string.IsNullOrEmpty(reqData.Minter) && reqData.Minter != "0x0000000000000000000000000000000000000000")
                {
                    activeRequestId = lastFoundRequestId.Value;
                    
                    if (reqData.IsFulfilled)
                    {
                        statusText.text = "You have an unclaimed system waiting!";
                        claimSystemButton.gameObject.SetActive(true);
                    }
                    else
                    {
                        statusText.text = "Pending request found. Waiting for Oracle...";
                        MonitorExistingRequest(activeRequestId); 
                    }
                    return; // Exit here, we successfully recovered the state
                }
            }

            // No pending requests found
            statusText.text = "Ready to discover.";
            discoverNewSystemButton.interactable = true;
        }
        catch (System.Exception e)
        {
            // If the RPC fails the history check, we fallback to ready state
            statusText.text = "Ready to discover.";
            Debug.LogError($"SYS Pending Check Error: {e.Message}");
            discoverNewSystemButton.interactable = true;
        }
    }

    private async void MonitorExistingRequest(BigInteger reqId)
    {
        var web3 = Web3AuthManager.Web3Instance;
        var contract = web3.Eth.GetContract(Web3Config.ContractABI, Web3Config.ContractAddress);
        var vrfRequestsFunc = contract.GetFunction("vrfRequests");

        try
        {
            bool isFulfilled = false;
            int attemptCounter = 0;

            while (!isFulfilled)
            {
                attemptCounter++;
                statusText.text = $"Pending request found.\nWaiting for Chainlink VRF... (Check {attemptCounter})";
                
                await Task.Delay(12000);
                
                try 
                {
                    var reqData = await vrfRequestsFunc.CallDeserializingToObjectAsync<PendingRequestDTO>(reqId);
                    if (reqData.IsFulfilled)
                    {
                        isFulfilled = true;
                    }
                }
                catch (System.Exception reqEx)
                {
                    Debug.LogWarning($"SYS RPC Polling timeout (Attempt {attemptCounter}): {reqEx.Message}");
                }
            }

            statusText.text = "Entropy received! Space coordinates calculated.";
            claimSystemButton.gameObject.SetActive(true);
        }
        catch (System.Exception e)
        {
            statusText.text = "Connection lost during monitoring.";
            discoverNewSystemButton.interactable = true;
            Debug.LogError($"SYS Monitor Error: {e.Message}");
        }
    }
}