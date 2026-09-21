using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

public class Web3AuthManager : MonoBehaviour
{
    [Header("UI Panels (The 3 States)")]
    public GameObject cardsPanel;
    public GameObject loginModalPanel;
    public GameObject web3HubPanel;

    [Header("UI Elements")]
    public Button cardConnectButton;
    public TMP_InputField privateKeyInput;
    public Button executeLoginButton;
    public TextMeshProUGUI feedbackText;

    [Header("Web3 Configuration")]
    private const string SEPOLIA_RPC_URL = "https://ethereum-sepolia-rpc.publicnode.com"; 
    private const int SEPOLIA_CHAIN_ID = 11155111;

    public static Account CurrentAccount { get; private set; }
    public static Web3 Web3Instance { get; private set; }

    private void Start()
    {
        Web3Config.LoadConfiguration();

        // Initial State: Show cards, hide everything else
        cardsPanel.SetActive(true);
        loginModalPanel.SetActive(false);
        web3HubPanel.SetActive(false);

        privateKeyInput.contentType = TMP_InputField.ContentType.Password;

        // Assign button listeners
        cardConnectButton.onClick.AddListener(ShowLoginModal);
        executeLoginButton.onClick.AddListener(AttemptConnection);
    }

    private void ShowLoginModal()
    {
        // Transition: Hide cards, show the login modal
        cardsPanel.SetActive(false);
        loginModalPanel.SetActive(true);
        feedbackText.text = ""; // Clear any previous messages
    }

    private async void AttemptConnection()
    {
        string pKey = privateKeyInput.text.Trim();

        if (string.IsNullOrEmpty(pKey) || (pKey.Length != 64 && pKey.Length != 66))
        {
            feedbackText.text = "Error: Invalid Private Key format.";
            return;
        }

        if (!pKey.StartsWith("0x"))
        {
            pKey = "0x" + pKey;
        }

        try
        {
            executeLoginButton.interactable = false;
            feedbackText.text = "Connecting to Sepolia Network...";

            // Initialize Nethereum
            CurrentAccount = new Account(pKey, SEPOLIA_CHAIN_ID);
            Web3Instance = new Web3(CurrentAccount, SEPOLIA_RPC_URL);

            // Network Test
            var balanceWei = await Web3Instance.Eth.GetBalance.SendRequestAsync(CurrentAccount.Address);
            var balanceEth = Web3.Convert.FromWei(balanceWei.Value);
            
            Debug.Log($"Connected! Address: {CurrentAccount.Address} | Balance: {balanceEth} ETH");
            
            // Success: Transition to the Web3 Hub
            loginModalPanel.SetActive(false);
            web3HubPanel.SetActive(true);
            FindFirstObjectByType<Web3InventoryManager>().LoadUserInventory();
            FindFirstObjectByType<Web3MintManager>().CheckForPendingRequests();
        }
        catch (Exception e)
        {
            feedbackText.text = "Connection failed. Check your key or internet connection.";
            Debug.LogError($"Web3 Initialization Error: {e.Message}");
            executeLoginButton.interactable = true;
        }
    }
}