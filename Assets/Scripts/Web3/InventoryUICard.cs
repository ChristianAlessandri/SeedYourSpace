using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System.Numerics;
using TMPro;

public class InventoryUICard : MonoBehaviour, IPointerClickHandler
{
    [Header("UI References")]
    public TextMeshProUGUI nameText;

    [Header("NFT Data")]
    public BigInteger systemSeed;
    public BigInteger algorithmVersion;

    public void SetupCard(BigInteger tokenId, BigInteger seed, BigInteger version)
    {
        systemSeed = seed;
        algorithmVersion = version;
        
        if (nameText != null)
        {
            nameText.text = "SYStem #" + tokenId.ToString();
        }
        else 
        {
            Debug.LogWarning("SYS: nameText reference is missing on the Card Prefab!");
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.clickCount == 2)
        {
            Debug.Log($"SYS: Warp to Seed {systemSeed}");
            SystemDataBridge.TargetSeed = systemSeed.ToString();
            SystemDataBridge.TargetVersion = (int)algorithmVersion;
            SceneManager.LoadScene("UniverseScene");
        }
    }
}