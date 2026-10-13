using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class TelemetryPayload
{
    public string session_id;
    public string game_id;
    public string player_name;
    public string device_id;
    public string platform;
    public int score;
    public int max_level;
    public int max_combo;
    public int lines_cleared;
    public int singles;
    public int doubles;
    public int triples;
    public int tetrises;
    public int blocks_spawned;
    public int block_i;
    public int block_o;
    public int block_t;
    public int block_l;
    public int block_j;
    public int block_s;
    public int block_z;
    public int total_moves;
    public int total_rotations;
    public int hard_drops;
    public int soft_drops;
    public int hold_count;
    public float duration_seconds;
    public float apm;
    public string status;
}

public class TelemetryManager : MonoBehaviour
{
    private static TelemetryManager _instance;
    public static TelemetryManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<TelemetryManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("TelemetryManager_Auto");
                    _instance = go.AddComponent<TelemetryManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    public string endpointUrl = "http://localhost:8080/api/telemetry";
    public string currentGameId = "";

    private TelemetryPayload currentData;
    private float sessionStartTime;
    private bool sessionEnded = false;
    private float lastSyncTime = 0f;
    private const float LiveSyncInterval = 2.0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        var _ = Instance;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (currentData == null)
        {
            StartNewSession();
        }
    }

    private void Update()
    {
        if (currentData != null && !sessionEnded && Time.unscaledTime - lastSyncTime >= LiveSyncInterval)
        {
            SyncLiveTelemetry();
        }
    }

    private void OnEnable()
    {
        GameManager.ScoreEvent += OnScoreChanged;
        GameManager.LevelEvent += OnLevelChanged;
        GameManager.ComboEvent += OnComboChanged;
    }

    private void OnDisable()
    {
        GameManager.ScoreEvent -= OnScoreChanged;
        GameManager.LevelEvent -= OnLevelChanged;
        GameManager.ComboEvent -= OnComboChanged;
    }

    private string GenerateGameId()
    {
        string shortGuid = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant();
        return $"OYUN-{shortGuid}";
    }

    public void StartNewSession()
    {
        sessionStartTime = Time.unscaledTime;
        sessionEnded = false;
        currentGameId = GenerateGameId();

        currentData = new TelemetryPayload
        {
            session_id = currentGameId,
            game_id = currentGameId,
            player_name = currentGameId,
            device_id = SystemInfo.deviceUniqueIdentifier,
            platform = Application.platform.ToString(),
            score = 0,
            max_level = 1,
            max_combo = 0,
            duration_seconds = 0f,
            apm = 0f,
            status = "active"
        };

        Debug.Log($"[Telemetry] Oyun basladi. Oyun ID: {currentGameId}");
        SyncLiveTelemetry();
    }

    private void OnScoreChanged(int newScore)
    {
        if (currentData == null) return;
        currentData.score = newScore;
    }

    private void OnLevelChanged(int newLevel)
    {
        if (currentData == null) return;
        if (newLevel > currentData.max_level)
            currentData.max_level = newLevel;
    }

    private void OnComboChanged(int newCombo)
    {
        if (currentData == null) return;
        if (newCombo > currentData.max_combo)
            currentData.max_combo = newCombo;
    }

    public void RecordBlockSpawn(string blockType)
    {
        if (currentData == null) StartNewSession();
        currentData.blocks_spawned++;

        switch (blockType)
        {
            case "I": currentData.block_i++; break;
            case "O": currentData.block_o++; break;
            case "T": currentData.block_t++; break;
            case "L": currentData.block_l++; break;
            case "J": currentData.block_j++; break;
            case "S": currentData.block_s++; break;
            case "Z": currentData.block_z++; break;
        }

        if (currentData.blocks_spawned == 1)
        {
            SyncLiveTelemetry();
        }
    }

    public void RecordMove(int colOffset, int rowOffset)
    {
        if (currentData == null) return;
        if (colOffset != 0)
            currentData.total_moves++;
        else if (rowOffset > 0)
            currentData.soft_drops++;
    }

    public void RecordRotation()
    {
        if (currentData == null) return;
        currentData.total_rotations++;
    }

    public void RecordHardDrop()
    {
        if (currentData == null) return;
        currentData.hard_drops++;
    }

    public void RecordHold()
    {
        if (currentData == null) return;
        currentData.hold_count++;
    }

    public void RecordLinesCleared(int lines)
    {
        if (currentData == null || lines <= 0) return;
        currentData.lines_cleared += lines;

        switch (lines)
        {
            case 1: currentData.singles++; break;
            case 2: currentData.doubles++; break;
            case 3: currentData.triples++; break;
            case 4: currentData.tetrises++; break;
            default: currentData.tetrises++; break;
        }
    }

    public void SyncLiveTelemetry()
    {
        if (currentData == null || sessionEnded) return;
        lastSyncTime = Time.unscaledTime;
        UpdateCalculatedMetrics("active");
        StartCoroutine(PostTelemetryCoroutine(JsonUtility.ToJson(currentData)));
    }

    public void CompleteSession()
    {
        if (currentData == null || sessionEnded) return;
        sessionEnded = true;
        UpdateCalculatedMetrics("completed");
        string json = JsonUtility.ToJson(currentData);
        Debug.Log($"[Telemetry] Oyun bitti ({currentGameId}). Skor={currentData.score}, APM={currentData.apm}");
        StartCoroutine(PostTelemetryCoroutine(json));
    }

    private void UpdateCalculatedMetrics(string status)
    {
        if (GameManager.Instance != null)
        {
            currentData.score = GameManager.Instance.score;
            if (GameManager.Instance.level > currentData.max_level)
                currentData.max_level = GameManager.Instance.level;
            if (GameManager.Instance.combo > currentData.max_combo)
                currentData.max_combo = GameManager.Instance.combo;
        }

        float elapsed = Mathf.Max(0f, Time.unscaledTime - sessionStartTime);
        currentData.duration_seconds = Mathf.Round(elapsed * 10f) / 10f;

        int totalActions = currentData.total_moves + currentData.total_rotations + currentData.hard_drops + currentData.soft_drops + currentData.hold_count;
        float apmElapsed = Mathf.Max(1f, elapsed);
        currentData.apm = totalActions > 0 ? Mathf.Round(((totalActions / apmElapsed) * 60f) * 10f) / 10f : 0f;
        currentData.status = status;
    }

    private IEnumerator PostTelemetryCoroutine(string jsonBody)
    {
        using (UnityWebRequest req = new UnityWebRequest(endpointUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 5;

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[Telemetry] Sunucu baglanti hatasi ({endpointUrl}): {req.error}");
            }
        }
    }

    private void OnApplicationQuit()
    {
        if (!sessionEnded && currentData != null)
        {
            sessionEnded = true;
            UpdateCalculatedMetrics("completed");
            UnityWebRequest req = new UnityWebRequest(endpointUrl, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(JsonUtility.ToJson(currentData));
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SendWebRequest();
        }
    }
}
