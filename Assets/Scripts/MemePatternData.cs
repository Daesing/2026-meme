using UnityEngine;
using UnityEngine.Serialization;

public enum WarningStyle
{
    Exclamation,
    Trajectory,
    Both
}

public enum MemeFormationType
{
    Single = 0,         // 기존 단일 스폰 (기본값)
    DualOpposite = 1,   // 좌우 양쪽에서 동시에 2개 등장 (서로 다른 높이 오프셋 적용 가능)
    DualParallel = 2,   // 같은 방향에서 상하 2줄로 동시에 2개 등장
    FanSpread = 3       // 한 지점에서 부채꼴 각도로 여러 발 동시 발사
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

    [Header("Multi-Spawn & Barrage")]
    [SerializeField] private MemeFormationType formation = MemeFormationType.Single;
    [SerializeField, Min(1)] private int projectileCount = 1;
    [SerializeField] private float dualHeightSeparation = 2.4f;
    [SerializeField] private float fanTotalAngle = 35f;
    [SerializeField, Min(0f)] private float burstInterval = 0f;
    [SerializeField] private float projectileScaleMultiplier = 1f;

    public MemeFormationType Formation => formation;
    public int ProjectileCount => Mathf.Max(1, projectileCount);
    public float DualHeightSeparation => dualHeightSeparation;
    public float FanTotalAngle => fanTotalAngle;
    public float BurstInterval => Mathf.Max(0f, burstInterval);
    public float ProjectileScaleMultiplier => projectileScaleMultiplier > 0f ? projectileScaleMultiplier : 1f;
}
