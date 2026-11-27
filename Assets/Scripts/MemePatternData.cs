using UnityEngine;
using UnityEngine.Serialization;

public enum WarningStyle
{
    Exclamation,
    Trajectory,
    Both
}

[CreateAssetMenu(fileName = "NewMemePattern", menuName = "MemeGame/Meme Pattern")]
public class MemePatternData : ScriptableObject
{
    [Header("Obstacle & Visual")]
    public MemeAttackObstacle obstaclePrefab;
    public Sprite overrideSprite;
    public Material overrideMaterial;

    [Header("Audio")]
    [FormerlySerializedAs("attackSound")]
    public AudioClip attackSfx;
    [Range(0f, 1f)] public float soundVolume = 1f;

    [Header("Movement")]
    [Min(0f)] public float moveSpeed = 10f;

    [Header("Warning Settings")]
    public WarningStyle warningStyle = WarningStyle.Trajectory;
    [Min(0.1f)] public float trajectoryThickness = 1.5f;
    public Color trajectoryColor = new Color(0f, 1f, 1f, 0.35f);
    [Min(1)] public int blinkCount = 3;

    [Header("Spawn Settings")]
    [Min(0.01f)] public float spawnWeight = 1f;
    public bool allowLeft = true;
    public bool allowRight = true;
    public bool allowCenter = true;

    [Header("Custom Position Override")]
    [Tooltip("체크하면 스포너 기본 좌표를 무시하고 아래 개별 좌표를 사용합니다.")]
    public bool useCustomPosition;
    public Vector2 customObstacleSpawnPos;
    public Vector2 customWarningSpawnPos;

    [Header("Report Card")]
    [Tooltip("게임 오버 성적표에 표시될 밈 이름")]
    public string memeTitle;
    [Tooltip("게임 오버 성적표에 표시될 탈락 한줄평")]
    [TextArea(2, 4)] public string failComment;
}
