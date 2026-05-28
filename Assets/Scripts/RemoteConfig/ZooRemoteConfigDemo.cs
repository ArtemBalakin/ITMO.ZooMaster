using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class ZooRemoteConfigDemo : MonoBehaviour
{
    [Serializable]
    public class ZooRemoteConfig
    {
        public string eventTitle = "Capybara Spa Week";
        public string eventSubtitle = "Clean the zoo and get x2 coins";
        public string themeHex = "#58B368";
        public float rewardMultiplier = 2f;
        public bool showEventBanner = true;
        public string callToAction = "Play event";
    }

    [Header("JSON source")]
    [SerializeField] private string configUrl = "https://jsonkeeper.com/b/REPLACE_ME";
    [SerializeField] private string fallbackFileName = "zoo_config_fallback.json";
    [SerializeField] private int timeoutSeconds = 7;
    [SerializeField] private bool loadOnStart = true;
    [SerializeField] private bool reloadWithF5 = true;

    [Header("Banner UI")]
    [SerializeField] private bool createBannerInRuntime = true;
    [SerializeField] private GameObject bannerRoot;
    [SerializeField] private Image bannerBackground;
    [SerializeField] private Text titleText;
    [SerializeField] private Text subtitleText;
    [SerializeField] private Text multiplierText;
    [SerializeField] private Text buttonText;

    private ZooRemoteConfig _currentConfig = new ZooRemoteConfig();
    private Coroutine _loadingCoroutine;

    private void Start()
    {
        if (createBannerInRuntime)
            CreateBannerIfNeeded();

        if (loadOnStart)
            ReloadConfig();
    }

    private void Update()
    {
        if (reloadWithF5 && Input.GetKeyDown(KeyCode.F5))
            ReloadConfig();
    }

    [ContextMenu("Reload config")]
    public void ReloadConfig()
    {
        if (_loadingCoroutine != null)
            StopCoroutine(_loadingCoroutine);

        _loadingCoroutine = StartCoroutine(LoadConfigRoutine());
    }

    private IEnumerator LoadConfigRoutine()
    {
        string json = null;

        if (CanUseRemoteUrl())
        {
            using (UnityWebRequest request = UnityWebRequest.Get(configUrl))
            {
                request.timeout = Mathf.Max(1, timeoutSeconds);
                yield return request.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
                bool failed = request.result != UnityWebRequest.Result.Success;
#else
                bool failed = request.isNetworkError || request.isHttpError;
#endif

                if (failed)
                    Debug.LogWarning("[ZooConfig] Remote config failed: " + request.error);
                else
                    json = request.downloadHandler.text;
            }
        }

        if (string.IsNullOrWhiteSpace(json))
            json = ReadFallbackJson();

        _currentConfig = ParseConfig(json);
        ApplyConfig(_currentConfig);
        _loadingCoroutine = null;
    }

    private bool CanUseRemoteUrl()
    {
        return !string.IsNullOrWhiteSpace(configUrl) && !configUrl.Contains("REPLACE_ME");
    }

    private string ReadFallbackJson()
    {
        string path = Path.Combine(Application.streamingAssetsPath, fallbackFileName);

        try
        {
            if (File.Exists(path))
                return File.ReadAllText(path);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[ZooConfig] Cannot read fallback json: " + exception.Message);
        }

        return JsonUtility.ToJson(new ZooRemoteConfig());
    }

    private ZooRemoteConfig ParseConfig(string json)
    {
        try
        {
            ZooRemoteConfig config = JsonUtility.FromJson<ZooRemoteConfig>(json);
            if (config == null)
                return new ZooRemoteConfig();

            if (string.IsNullOrWhiteSpace(config.eventTitle))
                config.eventTitle = "Zoo Event";
            if (string.IsNullOrWhiteSpace(config.eventSubtitle))
                config.eventSubtitle = "Remote config loaded";
            if (string.IsNullOrWhiteSpace(config.themeHex))
                config.themeHex = "#58B368";
            if (config.rewardMultiplier <= 0f)
                config.rewardMultiplier = 1f;

            return config;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[ZooConfig] Bad json: " + exception.Message);
            return new ZooRemoteConfig();
        }
    }

    private void ApplyConfig(ZooRemoteConfig config)
    {
        if (bannerRoot != null)
            bannerRoot.SetActive(config.showEventBanner);

        Color themeColor = new Color(0.31f, 0.62f, 0.39f, 0.92f);
        ColorUtility.TryParseHtmlString(config.themeHex, out themeColor);

        if (bannerBackground != null)
            bannerBackground.color = themeColor;
        if (titleText != null)
            titleText.text = config.eventTitle;
        if (subtitleText != null)
            subtitleText.text = config.eventSubtitle;
        if (multiplierText != null)
            multiplierText.text = "x" + config.rewardMultiplier.ToString("0.#") + " rewards";
        if (buttonText != null)
            buttonText.text = config.callToAction;

        Debug.Log("[ZooConfig] Applied: " + config.eventTitle + ", x" + config.rewardMultiplier);
    }

    private void CreateBannerIfNeeded()
    {
        if (bannerRoot != null && titleText != null && subtitleText != null)
            return;

#if UNITY_2023_1_OR_NEWER
        Canvas canvas = FindFirstObjectByType<Canvas>();
#else
        Canvas canvas = FindObjectOfType<Canvas>();
#endif
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("RemoteConfigCanvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            canvasObject.AddComponent<GraphicRaycaster>();
        }

        GameObject panel = new GameObject("RemoteEventBanner");
        panel.transform.SetParent(canvas.transform, false);
        bannerRoot = panel;

        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -28f);
        rect.sizeDelta = new Vector2(640f, 118f);

        bannerBackground = panel.AddComponent<Image>();
        bannerBackground.color = new Color(0.31f, 0.62f, 0.39f, 0.92f);

        titleText = CreateText(panel.transform, "Title", new Vector2(24f, -14f), new Vector2(430f, 34f), 26, FontStyle.Bold, TextAnchor.MiddleLeft);
        subtitleText = CreateText(panel.transform, "Subtitle", new Vector2(24f, -52f), new Vector2(430f, 28f), 17, FontStyle.Normal, TextAnchor.MiddleLeft);
        multiplierText = CreateText(panel.transform, "Multiplier", new Vector2(470f, -16f), new Vector2(140f, 34f), 24, FontStyle.Bold, TextAnchor.MiddleCenter);
        buttonText = CreateText(panel.transform, "ButtonText", new Vector2(470f, -58f), new Vector2(140f, 30f), 15, FontStyle.Bold, TextAnchor.MiddleCenter);
    }

    private static Text CreateText(Transform parent, string objectName, Vector2 position, Vector2 size, int fontSize, FontStyle style, TextAnchor anchor)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = anchor;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;

        return text;
    }
}
