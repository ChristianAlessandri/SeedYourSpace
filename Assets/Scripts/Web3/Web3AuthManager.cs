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
    public Button backButton;      
    public Button cardConnectButton;      
    public TMP_InputField privateKeyInput;
    public Button executeLoginButton;     
    public TextMeshProUGUI feedbackText;

    [Header("Status UI (Chip & Knob)")]
    public TextMeshProUGUI walletAddressText;
    public Image connectionKnobImage;
    public Color connectedColor = new Color(0.02f, 0.59f, 0.41f);
    public Color disconnectedColor = new Color(0.86f, 0.15f, 0.15f);

    [Header("Web3 Configuration")]
    private const string SEPOLIA_RPC_URL = "https://ethereum-sepolia-rpc.publicnode.com"; 
    private const int SEPOLIA_CHAIN_ID = 11155111;

    public static Account CurrentAccount { get; private set; }
    public static Web3 Web3Instance { get; private set; }

    private void Start()
    {
        if (walletAddressText != null) walletAddressText.text = "Disconnected";
        if (connectionKnobImage != null) connectionKnobImage.color = disconnectedColor;

        Web3Config.LoadConfiguration();

        cardsPanel.SetActive(true);
        loginModalPanel.SetActive(false);
        web3HubPanel.SetActive(false);

        backButton.onClick.AddListener(GoBack);
        cardConnectButton.onClick.AddListener(ShowLoginModal);
        executeLoginButton.onClick.AddListener(AttemptConnection);

        privateKeyInput.contentType = TMP_InputField.ContentType.Password;
    }

    private void GoBack()
    {
        cardsPanel.SetActive(true);
        loginModalPanel.SetActive(false);
    }

    private void ShowLoginModal()
    {
        cardsPanel.SetActive(false);
        loginModalPanel.SetActive(true);
        feedbackText.text = ""; 
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
            feedbackText.text = "Connecting to Sepolia Network...";

            CurrentAccount = new Account(pKey, SEPOLIA_CHAIN_ID);
            Web3Instance = new Web3(CurrentAccount, SEPOLIA_RPC_URL);

            var balanceWei = await Web3Instance.Eth.GetBalance.SendRequestAsync(CurrentAccount.Address);
            var balanceEth = Web3.Convert.FromWei(balanceWei.Value);
            
            if (walletAddressText != null) 
                walletAddressText.text = FormatAddress(CurrentAccount.Address);
            
            if (connectionKnobImage != null) 
                connectionKnobImage.color = connectedColor;

            loginModalPanel.SetActive(false);
            web3HubPanel.SetActive(true);

            privateKeyInput.text = "";

            FindFirstObjectByType<Web3InventoryManager>().LoadUserInventory();
            FindFirstObjectByType<Web3MintManager>().CheckForPendingRequests();
        }
        catch (Exception e)
        {
            feedbackText.text = "Connection failed. Check your key or internet connection.";
            Debug.LogError($"Web3 Initialization Error: {e.Message}");
        }
    }

    private string FormatAddress(string address)
    {
        if (string.IsNullOrEmpty(address) || address.Length < 10) return address;
        
        return $"{address.Substring(0, 6)}...{address.Substring(address.Length - 4)}";
    }
}