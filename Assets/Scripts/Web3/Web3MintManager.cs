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

    [Header("UI Elements")]
    public GameObject web3LoginPanel;

    [Header("Hub Interface")]
    public GameObject web3HubPanel;
    public Button backButton;
    public Button discoverNewSystemButton;
    public Button claimSystemButton;
    public TextMeshProUGUI statusText; 

    [Header("Confirmation Modal")]
    public GameObject confirmationModal;
    public TextMeshProUGUI feeBreakdownText;
    public Button confirmTransactionButton;
    public Button cancelTransactionButton;

    [Header("Status UI (Chip & Knob)")]
    public TextMeshProUGUI walletAddressText;
    public Image connectionKnobImage;
    public Color disconnectedColor = new Color(0.86f, 0.15f, 0.15f);

    // Stores the transaction input payload temporarily before execution.
    // Crucial for the two-step confirmation flow where estimation and execution are separated.
    private object cachedTxInput; 
    private BigInteger activeRequestId;

    // Tracks whether the user is confirming a Mint or a Claim operation in the shared modal.
    private enum TransactionType { None, Mint, Claim }
    private TransactionType pendingAction = TransactionType.None;

    // --- DATA TRANSFER OBJECTS (DTOs) ---
    // These DTOs define the structure for Nethereum to deserialize raw blockchain data (events and structs) into C# objects.

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
        
        backButton.onClick.AddListener(GoBack);
        confirmTransactionButton.onClick.AddListener(ExecutePendingTransaction);
        cancelTransactionButton.onClick.AddListener(CancelTransaction);
    }

    private void GoBack()
    {
        web3HubPanel.SetActive(false);
        web3LoginPanel.SetActive(true);
        if (walletAddressText != null) walletAddressText.text = "Disconnected";
        if (connectionKnobImage != null) connectionKnobImage.color = disconnectedColor;
    }

    // --- MINTING PHASE ---

    /// Constructs the raw minting transaction payload, requests gas estimation from the RPC node,
    /// applies safety buffers, and presents the cost breakdown to the user via the confirmation modal.
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
            
            // Extract ABI-encoded function data directly to bypass Nethereum's ambiguous overloaded methods
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

            // Apply a 20% buffer to the gas limit to prevent "Out of Gas" reverts during contract execution
            BigInteger safeGasLimit = (estimatedGas.Value * 120) / 100;
            // Apply a 15% buffer to the gas price to ensure priority inclusion by miners and avoid mempool stagnation
            BigInteger safeGasPrice = (currentGasPrice.Value * 115) / 100;

            // Inject the buffered values back into the payload before caching
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

    // --- CLAIMING PHASE ---

    /// Constructs the transaction payload to claim the generated NFT, estimates the required gas (0 ETH value),
    /// applies safety buffers, and routes the user to the confirmation modal.
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

    /// Routes the execution path based on the cached action type when the user confirms the transaction in the modal.
    private void ExecutePendingTransaction()
    {
        confirmationModal.SetActive(false);
        web3HubPanel.SetActive(true); 

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

    /// Broadcasts the signed mint transaction to the network and begins the asynchronous polling process.
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

    /// Broadcasts the final claim transaction and waits for network confirmation before updating the UI inventory.
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

            // Poll the RPC node until the transaction receipt is available, indicating the block is mined
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

    /// Orchestrates the two-part asynchronous monitoring:
    /// 1. Waits for the local mint transaction to be mined to extract the Request ID.
    /// 2. Polls the smart contract to check if Chainlink VRF has fulfilled the randomness request.
    private async void MonitorMintingProcess(string txHash)
    {
        var web3 = Web3AuthManager.Web3Instance;
        var contract = web3.Eth.GetContract(Web3Config.ContractABI, Web3Config.ContractAddress);

        statusText.text = "Transmitting coordinates... (Waiting for block confirmation)";

        try
        {
            // Await transaction mining
            Nethereum.RPC.Eth.DTOs.TransactionReceipt receipt = null;
            while (receipt == null)
            {
                await Task.Delay(4000);
                receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);
            }

            // Decode the logs to extract the unique requestId assigned by Chainlink Coordinator
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

            // Await Chainlink fulfillment
            while (!isFulfilled)
            {
                attemptCounter++;
                statusText.text = $"Link established (ID: {activeRequestId}).\nWaiting for Chainlink VRF... (Check {attemptCounter})";
                
                // Polling interval set to 12s to prevent RPC node rate-limiting and false-cached responses
                await Task.Delay(12000); 

                try
                {
                    // Check the contract state directly
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

    /// Scans the blockchain logs upon login to detect if the user has an unresolved mint request
    /// (e.g., they closed the app before claiming). Resumes the process gracefully if found.
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

            var currentBlock = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            
            // Constrain the historical search to 40,000 blocks to comply with public RPC node limits
            BigInteger fromBlockValue = currentBlock.Value - 40000;
            if (fromBlockValue < 0) fromBlockValue = 0;
            var fromBlock = new Nethereum.Hex.HexTypes.HexBigInteger(fromBlockValue);

            var eventHandler = web3.Eth.GetEvent<SystemRequestedEventDTO>(Web3Config.ContractAddress);
            var filter = eventHandler.CreateFilterInput(
                new Nethereum.RPC.Eth.DTOs.BlockParameter(fromBlock), 
                Nethereum.RPC.Eth.DTOs.BlockParameter.CreateLatest()
            );
            
            var events = await eventHandler.GetAllChangesAsync(filter);
            BigInteger? lastFoundRequestId = null;

            foreach (var ev in events)
            {
                if (ev.Event.Requester.ToLower() == userAddress.ToLower())
                {
                    lastFoundRequestId = ev.Event.RequestId;
                }
            }

            if (lastFoundRequestId.HasValue)
            {
                var vrfRequestsFunc = contract.GetFunction("vrfRequests");
                var reqData = await vrfRequestsFunc.CallDeserializingToObjectAsync<PendingRequestDTO>(lastFoundRequestId.Value);

                // Verify the request has not already been processed (minter address is not null)
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
                    return; 
                }
            }

            statusText.text = "Ready to discover.";
            discoverNewSystemButton.interactable = true;
        }
        catch (System.Exception e)
        {
            statusText.text = "Ready to discover.";
            Debug.LogError($"SYS Pending Check Error: {e.Message}");
            discoverNewSystemButton.interactable = true;
        }
    }

    /// Resumes the Chainlink polling loop for an existing unresolved request discovered during login.
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