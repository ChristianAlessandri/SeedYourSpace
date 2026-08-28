using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Contracts;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;

/// <summary>
/// Dummy interface for interacting with an Ethereum smart contract using Nethereum.
/// </summary>
public class NethereumDummy : MonoBehaviour
{
    [Header("Network Settings")]
    public string rpcUrl = "https://rpc.sepolia.org"; 
    public string contractAddress = "CONTRACT_ADDRESS";

    [Header("UI Elements")]
    public TMP_InputField privateKeyInputField;
    public TMP_InputField messageInputField;

    private Web3 web3;
    private Contract contract;
    private Account account;

    private readonly string abi = @"[{'inputs':[],'name':'lastMessage','outputs':[{'internalType':'string','name':'','type':'string'}],'stateMutability':'view','type':'function'},{'inputs':[{'internalType':'string','name':'_newMessage','type':'string'}],'name':'updateMessage','outputs':[],'stateMutability':'nonpayable','type':'function'}]";

    /// <summary>
    /// Establishes a connection to the Ethereum blockchain using the provided private key and initializes the contract instance.
    /// </summary>
    public void ConnectWallet()
    {
        string pk = privateKeyInputField.text.Trim();

        if (string.IsNullOrEmpty(pk))
        {
            Debug.LogError("Error: Key field is empty!");
            return;
        }

        try
        {
            // Initialize the account and web3 instance
            account = new Account(pk, 11155111);
            web3 = new Web3(account, rpcUrl);
            contract = web3.Eth.GetContract(abi.Replace("'", "\""), contractAddress);
            
            Debug.Log($"Connected successfully! Wallet Address: {account.Address}");
            
            // Clear the input field for security after login
            privateKeyInputField.text = ""; 
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error during connection: {e.Message}. Make sure the key is valid.");
        }
    }

    /// <summary>
    /// Reads the last message stored in the smart contract on the blockchain and logs it to the console.
    /// </summary>
    public async void ReadMessageFromBlockchain()
    {
        if (contract == null) { Debug.LogError("Error: You must connect first!"); return; }

        try
        {
            Debug.Log("Reading from blockchain...");
            var getFunction = contract.GetFunction("lastMessage");
            string message = await getFunction.CallAsync<string>();
            Debug.Log($"Message received: {message}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error in Read: {e.Message}");
        }
    }

    /// <summary>
    /// Writes a new message to the smart contract on the blockchain, signing the transaction with the connected wallet's private key.
    /// </summary>
    /// <param name="textToWrite">The new message string to be stored on-chain.</param>
    public async void WriteMessageToBlockchain(string textToWrite)
    {
        if (contract == null) { Debug.LogError("Error: You must connect first!"); return; }

        try
        {
            Debug.Log($"Building transaction...");
            var setFunction = contract.GetFunction("updateMessage");
            
            var txInput = setFunction.CreateTransactionInput(account.Address, textToWrite);
            txInput.Gas = new Nethereum.Hex.HexTypes.HexBigInteger(300000);
            
            Debug.Log("Transaction signed. Sending to Sepolia miners, please wait...");
            
            var receipt = await web3.Eth.TransactionManager.SendTransactionAndWaitForReceiptAsync(txInput);
            
            Debug.Log($"Transaction confirmed in block {receipt.BlockNumber}! Hash: {receipt.TransactionHash}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error in Write: {e.Message}");
        }
    }

    /// <summary>
    /// Triggered by the UI button, this method captures the input from the message field and initiates a blockchain write operation.
    /// </summary>
    public void OnWriteButtonClicked()
    {
        string newText = messageInputField.text;
        if (!string.IsNullOrEmpty(newText))
        {
            WriteMessageToBlockchain(newText);
            messageInputField.text = "";
        }
    }
}
