// Created by Alexander Tkachenko aka ALT, 2026 https://www.artstation.com/alternative_ms
// This solusion is optimized for WebGL build work
// version tag Compare1-Step-by-Step5-NamedFrames
using System;
using System.Collections.Generic;
using System.IO; // need for Path.GetFileNameWithoutExtension
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

public class AsepriteExporter : MonoBehaviour
{
    // get external method from plugin AsepriteFileExporter.jslib
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void DownloadFileFromUnity(string fileName, string base64Data);
#endif

    [Header("References")]
    [SerializeField] private AsepriteRuntimeLoader runtimeLoader;
    [SerializeField] private Button exportAtlasButton;
    [SerializeField] private Button exportFramesButton;

    private string currentFileName = "aseprite_file"; // file name without .aseprite

    private void Start()
    {
        if (exportAtlasButton != null) exportAtlasButton.onClick.AddListener(OnExportAtlasButtonClicked);
        if (exportFramesButton != null) exportFramesButton.onClick.AddListener(OnExportFramesButtonClicked);
    }

    public void SetCurrentFileName(string rawFileName)
    {
        if (string.IsNullOrEmpty(rawFileName)) return;

        // remove file extension "C:/Folder/player.ase" -->> "player"
        currentFileName = Path.GetFileNameWithoutExtension(rawFileName);
        Debug.Log($"[Exporter] Active file name set to: {currentFileName}");
    }

    private void OnExportAtlasButtonClicked()
    {
        List<Sprite> sprites = runtimeLoader.GetAnimationSprites();
        if (sprites == null || sprites.Count == 0) return;

        ExportAsHorizontalSpriteSheet(sprites);
    }

    private void OnExportFramesButtonClicked()
    {
        List<Sprite> sprites = runtimeLoader.GetAnimationSprites();
        if (sprites == null || sprites.Count == 0) return;

        ExportAsIndividualFrames(sprites);
    }

    private void ExportAsHorizontalSpriteSheet(List<Sprite> sprites)
    {
        try
        {
            int frameW = (int)sprites[0].rect.width;
            int frameH = (int)sprites[0].rect.height;
            int totalFrames = sprites.Count;

            Texture2D atlasTexture = new Texture2D(frameW * totalFrames, frameH, TextureFormat.RGBA32, false);
            atlasTexture.filterMode = FilterMode.Point;
            atlasTexture.wrapMode = TextureWrapMode.Clamp;

            for (int i = 0; i < totalFrames; i++)
            {
                Color32[] pixels = sprites[i].texture.GetPixels32();
                atlasTexture.SetPixels32(i * frameW, 0, frameW, frameH, pixels);
            }

            atlasTexture.Apply();
            byte[] pngBytes = atlasTexture.EncodeToPNG();
            Destroy(atlasTexture);

            // filename template: "aseprite-name_spritesheet.png"
            string atlasName = $"{currentFileName}_spritesheet.png";
            SendToBrowserDownload(atlasName, pngBytes);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Exporter] Atlas generation ERROR: {ex.Message}");
        }
    }

    private void ExportAsIndividualFrames(List<Sprite> sprites)
    {
        try
        {
            for (int i = 0; i < sprites.Count; i++)
            {
                Sprite sprite = sprites[i];
                int frameW = (int)sprite.rect.width;
                int frameH = (int)sprite.rect.height;

                Texture2D frameTexture = new Texture2D(frameW, frameH, TextureFormat.RGBA32, false);
                frameTexture.filterMode = FilterMode.Point;
                frameTexture.wrapMode = TextureWrapMode.Clamp;

                Color32[] pixels = sprite.texture.GetPixels32();
                frameTexture.SetPixels32(0, 0, frameW, frameH, pixels);
                frameTexture.Apply();

                byte[] pngBytes = frameTexture.EncodeToPNG();
                Destroy(frameTexture);

                // filename template: "aseprite-name_000.png"
                string fileName = $"{currentFileName}_{i:D3}.png";

                SendToBrowserDownload(fileName, pngBytes);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Exporter] Individual frames export ERROR: {ex.Message}");
        }
    }

    private void SendToBrowserDownload(string fileName, byte[] fileBytes)
    {
        string base64String = Convert.ToBase64String(fileBytes);

#if UNITY_EDITOR
        string savePath = UnityEditor.EditorUtility.SaveFilePanel("Save PNG", "", fileName, "png");
        if (!string.IsNullOrEmpty(savePath))
        {
            System.IO.File.WriteAllBytes(savePath, fileBytes);
            Debug.Log($"[Editor Exporter] Saved to: {savePath}");
        }
#elif UNITY_WEBGL && !UNITY_EDITOR
        DownloadFileFromUnity(fileName, base64String);
#endif
    }
}