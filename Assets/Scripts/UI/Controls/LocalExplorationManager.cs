using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class LocalExplorationManager : MonoBehaviour
{
    [Header("Settings")]
    public int algorithmVersion = 1;

    [Header("UI Panels")]
    public GameObject cardsPanel; 
    public GameObject seedInputPanel;   

    [Header("UI Elements")]
    public Button continueButton;
    public TMP_InputField seedInputField;
    public Button diceButton;
    public Button finalExploreButton;

    private void Start()
    {
        cardsPanel.SetActive(true);
        seedInputPanel.SetActive(false);

        continueButton.onClick.AddListener(ShowInputPanel);
        diceButton.onClick.AddListener(RandomizeSeedInput);
        finalExploreButton.onClick.AddListener(LaunchExploration);
    }

    private void ShowInputPanel()
    {
        cardsPanel.SetActive(false);
        seedInputPanel.SetActive(true);
        
        seedInputField.text = "";
    }

    private void RandomizeSeedInput()
    {
        string randomSeed = System.Guid.NewGuid().ToString().Substring(0, 8).ToUpper();
        seedInputField.text = randomSeed;
    }

    private void LaunchExploration()
    {
        string chosenSeed = seedInputField.text.Trim();

        if (string.IsNullOrEmpty(chosenSeed))
        {
            chosenSeed = System.Guid.NewGuid().ToString().Substring(0, 8).ToUpper();
        }

        SystemDataBridge.TargetSeed = chosenSeed;
        SystemDataBridge.TargetVersion = algorithmVersion;

        SceneManager.LoadScene("UniverseScene");
    }
}