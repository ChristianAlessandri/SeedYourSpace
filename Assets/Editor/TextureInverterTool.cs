using UnityEngine;
using UnityEditor;
using System.IO;

public class TextureInverterTool
{
    [MenuItem("Tools/UI/Invert Selected Icons (Black to White)")]
    public static void InvertSelectedTextures()
    {
        Object[] selectedObjects = Selection.GetFiltered(typeof(Texture2D), SelectionMode.Assets);

        if (selectedObjects.Length == 0)
        {
            Debug.LogWarning("[TextureInverter] Please select at least one Texture2D in the Project window.");
            return;
        }

        int processedCount = 0;

        foreach (Object obj in selectedObjects)
        {
            Texture2D originalTexture = (Texture2D)obj;
            string assetPath = AssetDatabase.GetAssetPath(originalTexture);

            // Ensure the texture is readable by Unity's CPU
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            if (importer != null && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            // Read original pixels (This works even on compressed textures if readable)
            Color[] pixels = originalTexture.GetPixels();
            
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i].r = 1.0f - pixels[i].r;
                pixels[i].g = 1.0f - pixels[i].g;
                pixels[i].b = 1.0f - pixels[i].b;
                // Alpha remains untouched
            }

            // CRITICAL FIX - Create a temporary uncompressed texture to accept the new pixels
            Texture2D tempTexture = new Texture2D(originalTexture.width, originalTexture.height, TextureFormat.RGBA32, false);
            tempTexture.SetPixels(pixels);
            tempTexture.Apply();

            // Encode the uncompressed texture to PNG and overwrite the original file
            byte[] bytes = tempTexture.EncodeToPNG();
            string fullPath = Application.dataPath + assetPath.Substring(6); 
            File.WriteAllBytes(fullPath, bytes);

            // Clean up memory to prevent leaks in the Editor
            Object.DestroyImmediate(tempTexture);
            
            processedCount++;
        }

        // Force Unity to refresh and reload the newly modified files
        AssetDatabase.Refresh();
        Debug.Log($"[TextureInverter] Successfully inverted {processedCount} icon(s)!");
    }
}