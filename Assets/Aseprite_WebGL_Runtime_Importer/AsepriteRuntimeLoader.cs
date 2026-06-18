// Created by Alexander Tkachenko aka ALT, 2026 https://www.artstation.com/alternative_ms
// This solusion is optimized for WebGL build work
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices; // need for WebGL build
using UnityEngine;

public class AsepriteRuntimeLoader : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void TriggerFileOpenDialog(string objectName, string methodName);
#endif

    [Header("References")]
    [SerializeField] private AsepriteReader asepriteReader;
    [SerializeField] private UnityEngine.UI.Button openFileButton;
    [SerializeField] private UnityEngine.UI.Image targetUiImage;
    [SerializeField] private int previewScale = 1;

    private Coroutine animationCoroutine;
    private List<Sprite> animationSprites = new List<Sprite>();
    private List<float> frameDurations = new List<float>();

    public AsepriteReader AsepriteReader
    {
        get => asepriteReader;
        set => asepriteReader = value;
    }

    private void Start()
    {
        if (openFileButton != null)
        {
            openFileButton.onClick.AddListener(OpenFileAsync);
        }
    }

    public void OpenFileAsync()
    {
        Debug.Log("OpenFileAsync()");
        StopRunningAnimation();
#if UNITY_EDITOR
        string path = UnityEditor.EditorUtility.OpenFilePanel("Select Aseprite file", "", "ase,aseprite");
        if (!string.IsNullOrEmpty(path))
        {
            byte[] fileBytes = System.IO.File.ReadAllBytes(path);
            OnAsepriteFileLoaded(Convert.ToBase64String(fileBytes));
        }
#elif UNITY_WEBGL && !UNITY_EDITOR
        Debug.Log("TriggerFileOpenDialog(...)");
        TriggerFileOpenDialog(gameObject.name, nameof(OnAsepriteFileLoaded));
#endif
    }

    public void OnAsepriteFileLoaded(string base64Data)
    {
        Debug.Log("OnAsepriteFileLoaded | base64Data : " + base64Data.Length);
        try
        {
            byte[] fileBytes = Convert.FromBase64String(base64Data);

            if (asepriteReader == null)
            {
                Debug.LogError($"[{name}] Ошибка: AsepriteReader не назначен в инспекторе!", this);
                return;
            }

            Debug.Log("try to AsepriteReader.AseFileHeader header...");
            AsepriteReader.AseFileHeader header = asepriteReader.ReadHeader(fileBytes);

            animationSprites.Clear();
            frameDurations.Clear();

            // preparing frames one by one
            for (int i = 0; i < header.TotalFrames; i++)
            {
                Debug.Log("i : " + i + " | " + header.TotalFrames);

                // get durations and pixels on for current frame
                float duration;
                Color32[] singleFramePixels = asepriteReader.ParseFramePixels(fileBytes, i, header.Width, header.Height, out duration);

                Texture2D texture = new Texture2D(header.Width, header.Height, TextureFormat.RGBA32, false);
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.SetPixels32(singleFramePixels);
                texture.Apply();

                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, header.Width, header.Height), new Vector2(0.5f, 0.5f), 100f);
                animationSprites.Add(sprite);
                frameDurations.Add(duration);

                Debug.Log("duration : " + duration);
            }

            Debug.Log($"[Loader] Animation loaded step-by-step. Frames: {animationSprites.Count}");

            if (targetUiImage != null && animationSprites.Count > 0)
            {
                targetUiImage.color = Color.white;
                targetUiImage.enabled = true;
                targetUiImage.transform.localScale = Vector3.one;
                targetUiImage.rectTransform.sizeDelta = new Vector2(header.Width * previewScale, header.Height * previewScale);
                animationCoroutine = StartCoroutine(PlayAsepriteAnimation());
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Loader] Step Loader ERROR: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private IEnumerator PlayAsepriteAnimation()
    {
        Debug.Log("PlayAsepriteAnimation()");
        int currentFrame = 0;
        while (true)
        {
            targetUiImage.sprite = animationSprites[currentFrame];
            yield return new WaitForSeconds(frameDurations[currentFrame]);
            currentFrame = (currentFrame + 1) % animationSprites.Count;
        }
    }

    private void StopRunningAnimation()
    {
        Debug.Log("StopRunningAnimation()");
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
        }
    }

    public List<Sprite> GetAnimationSprites()
    {
        return animationSprites;
    }
}