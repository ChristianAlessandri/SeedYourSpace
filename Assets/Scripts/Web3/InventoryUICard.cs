using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System.Numerics;
using TMPro;
using System.Text.RegularExpressions;

public class InventoryUICard : MonoBehaviour, IPointerClickHandler
{
    [Header("UI References")]
    public TextMeshProUGUI nameText; 
    public TextMeshProUGUI seedText;
    public Image nftIconImage; 

    [Header("NFT Data")]
    public BigInteger systemSeed;
    public BigInteger algorithmVersion;

    public void SetupCard(BigInteger tokenId, BigInteger seed, BigInteger version, string rawSvgString)
    {
        systemSeed = seed;
        algorithmVersion = version;
        
        if (nameText != null)
        {
            nameText.text = "SYStem #" + tokenId.ToString();
        }

        if (seedText != null)
        {
            seedText.text = FormatSeed(seed);
        }

        if (nftIconImage != null && !string.IsNullOrEmpty(rawSvgString))
        {
            Sprite generatedSprite = GenerateSpriteFromSVG(rawSvgString);
            if (generatedSprite != null)
            {
                nftIconImage.sprite = generatedSprite;
            }
        }
    }

    private string FormatSeed(BigInteger seed)
    {
        string hexSeed = seed.ToString("x");
        if (hexSeed.Length <= 8) return "0x" + hexSeed;

        return $"0x{hexSeed.Substring(0, 4)}...{hexSeed.Substring(hexSeed.Length - 4)}";
    }

    private Sprite GenerateSpriteFromSVG(string svgString)
    {
        MatchCollection matches = Regex.Matches(svgString, @"fill=""#([0-9a-fA-F]{6})""");

        if (matches.Count == 25)
        {
            Texture2D tex = new Texture2D(5, 5);
            tex.filterMode = FilterMode.Point; 

            for (int i = 0; i < 25; i++)
            {
                string hexColor = "#" + matches[i].Groups[1].Value;
                if (ColorUtility.TryParseHtmlString(hexColor, out Color color))
                {
                    int x = i % 5;
                    int y = 4 - (i / 5); 
                    tex.SetPixel(x, y, color);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 5, 5), new UnityEngine.Vector2(0.5f, 0.5f));
        }

        Debug.LogWarning("Warning: The returned SVG does not contain 25 valid colors.");
        return null;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.clickCount == 2)
        {
            SystemDataBridge.TargetSeed = systemSeed.ToString();
            SystemDataBridge.TargetVersion = (int)algorithmVersion;
            SceneManager.LoadScene("UniverseScene");
        }
    }
}