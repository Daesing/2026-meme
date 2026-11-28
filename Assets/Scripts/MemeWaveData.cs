using System.Collections.Generic;
using UnityEngine;

public enum SpawnSideOverride
{
    Left,
    Right,
    Top,
    RandomHorizontal
}

[CreateAssetMenu(fileName = "NewMemeWave", menuName = "MemeGame/Meme Wave Data")]
public class MemeWaveData : ScriptableObject
{
    [System.Serializable]
    public class WaveStep
    {
        [Tooltip("출격시킬 기존 단품 밈 패턴 에셋 (MemePatternData)")]
        public MemePatternData pattern;

        [Tooltip("웨이브 시작 후 몇 초 뒤에 이 패턴을 발사할지 (0이면 즉시 동시 출격)")]
        [Min(0f)] public float delayFromWaveStart = 0f;

        [Tooltip("체크하면 패턴 기본 방향 대신 아래 스폰 방향 설정을 사용합니다.")]
        public bool overrideSpawnSide = false;

        [Tooltip("overrideSpawnSide 체크 시 적용할 스폰 방향")]
        public SpawnSideOverride spawnSideOverride = SpawnSideOverride.RandomHorizontal;

        [Tooltip("체크하면 0.0~1.0 정규화 높이로 스폰 위치를 지정합니다. (0.15 바닥 슬라이딩, 0.5 중단, 0.8 상단 공중)")]
        public bool overrideNormalizedHeight = false;

        [Tooltip("정규화 스폰 높이 (0.0 = 최하단, 0.5 = 중단, 1.0 = 최상단)")]
        [Range(0f, 1f)] public float normalizedHeight = 0.5f;

        [Tooltip("코스 연출용 개별 이동 속도 배율 (기본값 1)")]
        [Min(0.1f)] public float speedMultiplier = 1f;

        public bool IsValid => pattern != null && pattern.obstaclePrefab != null;
        public float EffectiveSpeedMultiplier => speedMultiplier > 0f ? speedMultiplier : 1f;
    }

    [Header("웨이브 메타데이터")]
    [Tooltip("코스 이름 (예: 상하 봉쇄 코스, 예 아니오 티키타카)")]
    public string waveName;

    [Tooltip("이 점수 이상일 때부터 셔플백 후보에 해금됩니다.")]
    [Min(0)] public int minUnlockScore = 0;

    [Tooltip("웨이브 추첨 가중치")]
    [Min(0.01f)] public float weight = 1f;

    [Tooltip("이 코스가 끝난 뒤 다음 코스 시작 전 휴식 시간(초)")]
    [Min(0f)] public float postWaveDelay = 1.2f;

    [Header("테마 BGM 설정")]
    [Tooltip("이 웨이브 시작 시 재생할 테마 BGM. 비어 있으면 현재 재생 중인 BGM을 유지합니다.")]
    [SerializeField] private AudioClip waveBgm;

    [Tooltip("체크 시 이 웨이브가 끝난 뒤 기본 mainBgm으로 복귀합니다. 해제 시 다음 테마 BGM이 나올 때까지 유지됩니다.")]
    [SerializeField] private bool restoreMainBgmOnEnd = false;

    [Header("코스 출격 단계")]
    [Tooltip("웨이브 내에서 순차/동시 출격할 단품 패턴 단계 목록")]
    public List<WaveStep> steps = new List<WaveStep>();

    public AudioClip WaveBgm => waveBgm;
    public bool RestoreMainBgmOnEnd => restoreMainBgmOnEnd;

    public bool HasValidStep()
    {
        if (steps == null || steps.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i] != null && steps[i].IsValid)
            {
                return true;
            }
        }

        return false;
    }
}
