// Created by Alexander Tkachenko aka ALT, 2026 https://www.artstation.com/alternative_ms
// This solusion is optimized for WebGL build work
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices; // need for WebGL build
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
    [SerializeField] private Button exportButton;

    private void Start()
    {
        if (exportButton != null) exportButton.onClick.AddListener(OnExportButtonClicked);
    }

    private void OnExportButtonClicked()
    {
        List<Sprite> sprites = runtimeLoader.GetAnimationSprites();

        if (sprites == null || sprites.Count == 0)
        {
            Debug.LogWarning("[Exporter] No data to export");
            return;
        }

        ExportAsHorizontalSpriteSheet(sprites);
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

            SendToBrowserDownload("unity-aseprite_spritesheet.png", pngBytes);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Exporter] Atlas generation ERROR: {ex.Message}");
        }
    }

    private void SendToBrowserDownload(string fileName, byte[] fileBytes)
    {
        string base64String = Convert.ToBase64String(fileBytes);

#if UNITY_EDITOR
        // show OS based file dialog
        string savePath = UnityEditor.EditorUtility.SaveFilePanel("Save PNG atlas", "", fileName, "png");

        if (!string.IsNullOrEmpty(savePath))
        {
            System.IO.File.WriteAllBytes(savePath, fileBytes);
            Debug.Log($"[Editor Exporter] File saved to: {savePath}");
        }
#elif UNITY_WEBGL && !UNITY_EDITOR
    // in WebGL call JS-logic to open file dialog
    DownloadFileFromUnity(fileName, base64String);
#endif
    }

}