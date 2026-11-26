using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

public class SubtitleSpawner : MonoBehaviour
{
    private struct SubtitleEntry
    {
        public SubtitleTag Tag;
        public float SpeedScale;
        public string Text;
    }

    [Header("Data Source (Assets/Resources)")]
    [SerializeField] private string resourceFileName = "subtitles";

    [Header("Prefab & Font")]
    [SerializeField] private SubtitleObstacle obstaclePrefab = null;
    [SerializeField] private TMP_FontAsset fontAsset = null;

    [Header("Spawn Settings")]
    [SerializeField] private float spawnInterval = 1.6f;
    [SerializeField] private float baseFallSpeed = 3.5f;
    [SerializeField, Range(0f, 0.9f)] private float speedRandomVariance = 0.5f;
    [SerializeField] private float spawnY = 6.5f;
    [SerializeField] private float minSpawnX = -4.5f;
    [SerializeField] private float maxSpawnX = 4.5f;
    [SerializeField] private float wallMaxAbsX = 2.0f;
    [SerializeField] private bool spawnOnStart = true;

    private readonly List<SubtitleEntry> entries = new List<SubtitleEntry>();
    private float spawnTimer;

    private void Start()
    {
        LoadSubtitles();

        if (entries.Count == 0)
        {
            Debug.LogWarning($"[SubtitleSpawner] 자막 데이터를 찾지 못했습니다. Assets/Resources/{resourceFileName}.txt 파일을 확인하세요.");
            return;
        }

        if (spawnOnStart)
        {
            SpawnRandomSubtitle();
            spawnTimer = 0f;
        }
    }

    private void Update()
    {
        if (entries.Count == 0)
        {
            return;
        }

        spawnTimer += Time.deltaTime;
        if (spawnTimer >= spawnInterval)
        {
            spawnTimer = 0f;
            SpawnRandomSubtitle();
        }
    }

    public void SpawnRandomSubtitle()
    {
        if (entries.Count == 0)
        {
            return;
        }

        SubtitleEntry entry = entries[UnityEngine.Random.Range(0, entries.Count)];
        float spawnX = entry.Tag == SubtitleTag.Wall
            ? UnityEngine.Random.Range(-wallMaxAbsX, wallMaxAbsX)
            : UnityEngine.Random.Range(minSpawnX, maxSpawnX);
        Vector3 spawnPosition = new Vector3(spawnX, spawnY, 0f);

        SubtitleObstacle obstacle;
        if (obstaclePrefab != null)
        {
            obstacle = Instantiate(obstaclePrefab, spawnPosition, Quaternion.identity);
        }
        else
        {
            GameObject obstacleObj = new GameObject($"Subtitle_{entry.Tag}");
            obstacleObj.transform.position = spawnPosition;
            obstacle = obstacleObj.AddComponent<SubtitleObstacle>();
        }

        float randomSpeedMultiplier = UnityEngine.Random.Range(
            1f - speedRandomVariance,
            1f + speedRandomVariance
        );

        obstacle.Initialize(
            entry.Text,
            entry.Tag,
            baseFallSpeed,
            entry.SpeedScale,
            randomSpeedMultiplier,
            fontAsset
        );
    }

    private void LoadSubtitles()
    {
        entries.Clear();

        TextAsset textAsset = LoadSubtitleTextAsset();
        if (textAsset == null)
        {
            return;
        }

        string[] lines = textAsset.text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
            {
                continue;
            }

            SubtitleEntry parsed = ParseLine(trimmed);
            if (!string.IsNullOrEmpty(parsed.Text))
            {
                entries.Add(parsed);
            }
        }
    }

    private TextAsset LoadSubtitleTextAsset()
    {
        TextAsset asset = Resources.Load<TextAsset>(resourceFileName);
        if (asset != null)
        {
            return asset;
        }

        string[] fallbacks = { "Subtitle", "Subtitles", "subtitle" };
        for (int i = 0; i < fallbacks.Length; i++)
        {
            if (string.Equals(fallbacks[i], resourceFileName, StringComparison.Ordinal))
            {
                continue;
            }

            asset = Resources.Load<TextAsset>(fallbacks[i]);
            if (asset != null)
            {
                return asset;
            }
        }

        return null;
    }

    private static SubtitleEntry ParseLine(string line)
    {
        string[] parts = line.Split('|');

        // 형식 1: 유형|속도배율|자막내용 (예: FAST|1.8|억까 멈춰!!)
        if (parts.Length >= 3 && TryParseTag(parts[0], out SubtitleTag parsedTag))
        {
            float speedScale = -1f;
            if (float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedSpeed) && parsedSpeed > 0f)
            {
                speedScale = parsedSpeed;
            }

            string text = string.Join("|", parts, 2, parts.Length - 2).Trim();
            return new SubtitleEntry
            {
                Tag = parsedTag,
                SpeedScale = speedScale,
                Text = text
            };
        }

        // 형식 2: 유형|자막내용 (예: FAST|억까 멈춰!!)
        if (parts.Length == 2 && TryParseTag(parts[0], out SubtitleTag twoPartTag))
        {
            return new SubtitleEntry
            {
                Tag = twoPartTag,
                SpeedScale = -1f,
                Text = parts[1].Trim()
            };
        }

        // 형식 3: 자막내용만 있는 줄
        return new SubtitleEntry
        {
            Tag = SubtitleTag.Normal,
            SpeedScale = 1f,
            Text = line
        };
    }

    private static bool TryParseTag(string rawTag, out SubtitleTag tag)
    {
        switch (rawTag.Trim().ToUpperInvariant())
        {
            case "FAST":
                tag = SubtitleTag.Fast;
                return true;
            case "WALL":
                tag = SubtitleTag.Wall;
                return true;
            case "ZIGZAG":
                tag = SubtitleTag.Zigzag;
                return true;
            case "HEAVY":
                tag = SubtitleTag.Heavy;
                return true;
            case "NORMAL":
                tag = SubtitleTag.Normal;
                return true;
            default:
                tag = SubtitleTag.Normal;
                return false;
        }
    }
}
