# AGENTS.md — 2026-meme Unity 프로젝트 AI 작업 규칙

이 문서는 본 Unity 2D 프로젝트(`2026-meme`)에서 AI 에이전트가 코드를 분석·수정·추가할 때 반드시 준수해야 하는 아키텍처 요약과 작업 규칙을 정의합니다.

---

## 1. 프로젝트 폴더 및 핵심 파일 구조

### `Assets/Scripts/`
- `Assets/Scripts/GameManager.cs`
- `Assets/Scripts/MemePatternData.cs`
- `Assets/Scripts/MemeWaveData.cs`
- `Assets/Scripts/MemeAttackSpawner.cs`
- `Assets/Scripts/MemeAttackObstacle.cs`
- `Assets/Scripts/HitFeelController.cs`
- `Assets/Scripts/PlayerBoxController.cs`
- `Assets/Scripts/AudioManager.cs`
- `Assets/Scripts/CameraFollow2D.cs`
- `Assets/Scripts/UI/ResultReportUI.cs`

### `Assets/Prefabs/`
- `Assets/Prefabs/UmmObstacle.prefab` — 밈 장애물 프리팹
- `Assets/Prefabs/YahoObstacle.prefab` — 밈 장애물 프리팹
- `Assets/Prefabs/WarningMark.prefab` — 느낌표(`!`) 경고 프리팹
- `Assets/Prefabs/WarningTrajectory.prefab` — 공격 궤적 반투명 띠 경고 프리팹

### 기타 주요 폴더 (참고)
- `Assets/Patterns/` — `MemePatternData` ScriptableObject 에셋 (`UmmPattern.asset`, `YahoPattern.asset`)
- `Assets/Scenes/` — `Main.unity`
- `Assets/Editor/` — 에디터 셋업 툴 (`MemeSetupTool.cs`, `ResultReportUITool.cs`)

---

## 2. 현재 프로젝트 아키텍처 요약

### 핵심 시스템 스크립트
- **`GameManager`** (`Assets/Scripts/GameManager.cs`)
  - 싱글톤(`GameManager.Instance`)으로 게임 전체 상태(`isGameOver`), 플레이어 체력(`maxHp`, `currentHp`), 생존 시간(`survivalTime`), 점수(`score`, `scorePerSecond` 기반)를 총괄합니다.
  - `MemeAttackObstacle.OnPlayerHit` 정적 이벤트를 구독하여 피격 처리(`HandleMemeAttackHit` → `ApplyPlayerDamage`)와 게임오버 시퀀스(`TriggerGameOver` → `GameOverSequenceRoutine`)를 실행합니다.
  - 게임오버 시 플레이어 입력 비활성화, 스포너 중단, 히트 필 연출 대기, `Time.timeScale = 0f` 적용, `ResultReportUI.ShowReport` 호출 및 `OnGameOver` / `OnGameOverResultReady` 이벤트를 발행합니다.
  - **중요: 별도의 `PlayerHealth` 스크립트는 존재하지 않으며, 체력 시스템은 `GameManager`에 통합되어 있습니다. 별도의 체력 스크립트를 새로 만들지 마세요.**

- **`MemePatternData`** (`Assets/Scripts/MemePatternData.cs`)
  - 장애물 패턴을 정의하는 `ScriptableObject` (`CreateAssetMenu: MemeGame/Meme Pattern`)입니다.
  - 주요 데이터: 장애물 프리팹/비주얼(`obstaclePrefab`, `overrideSprite`, `overrideMaterial`), 사운드(`attackSfx`, `soundVolume`), 이동 속도(`moveSpeed`), 경고 설정(`warningStyle` [`WarningStyle`: `Exclamation` / `Trajectory` / `Both`], `trajectoryThickness`, `trajectoryColor`, `blinkCount`), 스폰 설정(`spawnWeight`, `allowLeft`, `allowRight`, `allowCenter`, `useCustomPosition`, `customObstacleSpawnPos`, `customWarningSpawnPos`), 성적표 정보(`memeTitle`, `failComment`).

- **`MemeWaveData`** (`Assets/Scripts/MemeWaveData.cs`)
  - 기존 단품 `MemePatternData` 에셋을 조합해 '세트 코스요리' 형태의 웨이브를 정의하는 `ScriptableObject` (`CreateAssetMenu: MemeGame/Meme Wave Data`)입니다.
  - 메타데이터: `waveName`, `minUnlockScore`(점수 해금 조건), `weight`(가중치), `postWaveDelay`(코스 종료 후 휴식 시간).
  - 출격 단계(`List<WaveStep> steps`): `pattern`, `delayFromWaveStart`(0이면 즉시 동시 출격), `overrideSpawnSide` + `spawnSideOverride`(`Left`/`Right`/`Top`/`RandomHorizontal`), `overrideNormalizedHeight` + `normalizedHeight`(0.0~1.0 정규화 Y 높이), `speedMultiplier`(코스 연출용 개별 속도 배율).

- **`MemeAttackSpawner`** (`Assets/Scripts/MemeAttackSpawner.cs`)
  - **웨이브 모드(`waves`) 및 단품 폴백(`patterns`)**: `waves`에 유효한 `MemeWaveData`가 1개 이상 등록되어 있으면 웨이브 모드로 동작하며, 비어 있으면 기존 `patterns` 단품 모드(`MemeOrderMode`: `ShuffleBag` / `Sequential` / `WeightedRandom`)로 자동 폴백합니다.
  - 웨이브 모드에서는 `GameManager.Instance.Score`를 기준으로 `minUnlockScore <= 현재점수`를 만족하는 웨이브들로 가중치 반영 셔플백을 구성해 순환 실행하며(직전 웨이브 연속 출현 방지 및 해금 시 즉시 편입), 각 `WaveStep`의 `delayFromWaveStart`·방향/높이 오버라이드·`speedMultiplier`를 적용해 동시/순차 출격합니다.
  - **점수 기반 난이도 상승(`Score Difficulty Scaling`)**: 점수가 `maxDifficultyScore`에 가까워질수록 `postWaveDelay`가 점진적으로 단축(`minPostWaveDelayScale` 및 `minPostWaveDelay` 하한선 적용)되고, 장애물 이동 속도 배율이 최대 `maxDifficultySpeedMultiplier`(기본 1.6배)까지 상승합니다.
  - 공격 전 `WarningStyle`에 따라 점멸 경고 연출(기본 3회 점멸, `blinkCount` 적용)을 수행하며 `WarningTrajectory`(`WarningTrajectory.prefab`)와 `WarningMark`(`WarningMark.prefab` 또는 런타임 TMP 경고 텍스트)를 스텝별로 안전하게 제어하고, `StopSpawning()`으로 게임 오버 시 모든 활성 스텝 코루틴과 경고를 정리합니다.

- **`MemeAttackObstacle`** (`Assets/Scripts/MemeAttackObstacle.cs`)
  - `MemeSpawnType`(`Left`, `Right`, `Center`)에 따라 장애물 이동(좌/우 수평 대시, 중앙 하강 및 스케일 확대)과 화면 이탈 시 자동 파괴를 처리합니다.
  - 원본 `MemePatternData` 참조(`SourcePattern`)를 보유하여 피격 시 탈락 주범 정보를 전달합니다.
  - 스포너에서 속도 배율(`speedMultiplier`)과 좌표를 깔끔하게 전달할 수 있도록 `Initialize(..., float baseMoveSpeed, float speedMultiplier, ...)` 오버로드 및 `SetSpawnPosition`, `SetSpawnCoordinates`, `ApplySpeedMultiplier` 보조 메서드를 제공합니다.
  - `SpriteRenderer`, `BoxCollider2D`(Trigger), `Rigidbody2D`(Kinematic)를 요구하며, 플레이어 충돌 감지 시 정적 이벤트 `OnPlayerHit` 및 `UnityEvent`를 발행합니다.

- **`HitFeelController`** (`Assets/Scripts/HitFeelController.cs`)
  - `Main Camera`에 부착되어 피격 연출을 담당합니다(미부착 시 `GameManager`가 `Camera.main`에서 탐색하거나 동적으로 부착).
  - `PlayHitFeedback` 코루틴을 통해 히트스톱(`Time.timeScale = 0f` + `WaitForSecondsRealtime`) 후 `Time.unscaledDeltaTime` 기반 카메라 흔들림을 수행하며, 비활성화/파괴 시 `Time.timeScale` 및 카메라 위치를 원복(`RestoreState`)합니다.

- **`ResultReportUI`** (`Assets/Scripts/UI/ResultReportUI.cs`)
  - 게임 오버 시 **'2026 밈 능력 검정시험 성적통지표'** UI를 출력(`ShowReport`)합니다.
  - 점수 기준에 따른 급수 판정(`불합격`, `3급`, `2급`, `1급`, `특급`), `PlayerPrefs`(`MemeLicense_BestScore`) 기반 최고 기록 갱신/표시, 탈락 주범 `MemePatternData`의 스프라이트/`memeTitle`/`failComment` 표시, 재도전 버튼(`retryButton` → `GameManager.RestartGame()`) 바인딩을 담당합니다.

### 보조 시스템 스크립트
- **`PlayerBoxController`** (`Assets/Scripts/PlayerBoxController.cs`): 2D 플레이어 좌우 이동(`A`/`D`)과 다단 점프(`W`/`Space`, `maxJumps`), `BoxCastAll` 기반 지면 판정, `SetInputEnabled(bool)`을 통한 게임 오버 시 입력 차단을 담당합니다.
- **`AudioManager`** (`Assets/Scripts/AudioManager.cs`): 싱글톤(`AudioManager.Instance`, `DontDestroyOnLoad`)으로 BGM 및 공통 SFX(`PlayWarningSFX`, `PlayJumpSFX`, `PlayHitSFX`, `PlaySFX`) 재생을 담당합니다.
- **`CameraFollow2D`** (`Assets/Scripts/CameraFollow2D.cs`): Orthographic 2D 카메라가 `target`을 지정된 `offset`으로 추적하도록 처리합니다.

### 이벤트 및 데이터 흐름 요약
1. `MemeAttackSpawner` → (`waves` 등록 시) 점수 해금 조건(`minUnlockScore`) 기반 `MemeWaveData` 셔플백 선택 → 각 `WaveStep` 타이밍(`delayFromWaveStart`)·방향/정규화 높이 오버라이드·점수 난이도 속도 배율 적용 → 경고 연출(`WarningTrajectory` / `WarningMark`) → `MemeAttackObstacle` 스폰 및 `SourcePattern` 주입 (`waves` 미등록 시 기존 `patterns` 단품 모드로 폴백)
2. `MemeAttackObstacle` 플레이어 충돌 감지 → `MemeAttackObstacle.OnPlayerHit` 발행
3. `GameManager` 수신 → HP 차감 및 `AudioManager.PlayHitSFX()`
   - 생존 시: `HitFeelController.PlayHitFeedback()` 호출
   - HP 0 시: `TriggerGameOver(sourcePattern)` → 입력 차단 & 스포너 중단(활성 웨이브 스텝 코루틴/경고 정리) → `HitFeelController` 연출 → `Time.timeScale = 0f` → `ResultReportUI.ShowReport(...)` 출력

---

## 3. 절대 수정 금지 규칙 (Strict Rules)

1. **직렬화된 Unity 에셋/씬/프리팹 수치 무단 수정 금지**
   - 사용자가 명시적으로 요청하지 않는 한 `.unity`, `.prefab`, `.asset` 파일의 직렬화된 인스펙터 수치(`Transform`, `Scale`, `Speed`, `Position`, 컴포넌트 참조 연결 등)를 **절대 수정하지 마세요**.
   - 코드 변경 시 기존 `[SerializeField]` 필드명을 임의로 변경하여 인스펙터 값이나 참조가 유실되지 않도록 주의하세요(불가피한 경우 `[FormerlySerializedAs]` 사용).

2. **중복·불필요한 새 스크립트 임의 생성 금지**
   - 새로운 스크립트를 만들기 전에 반드시 `Assets/Scripts/`의 기존 스크립트를 먼저 확인하세요.
   - `GameManager`에 이미 체력/피격/게임오버 로직이 구현되어 있으므로 `PlayerHealth` 같은 중복·불필요한 새 스크립트를 임의로 생성하지 마세요.
   - 기존 클래스의 책임 범위에 속하는 기능은 새 클래스를 난립시키지 말고 기존 아키텍처에 자연스럽게 확장하세요.

3. **시간 정지(`Time.timeScale = 0f`) 환경 유의**
   - 히트스톱(`HitFeelController`) 및 게임 오버 상태(`GameManager`)에서 `Time.timeScale = 0f`가 사용됩니다.
   - 피격 연출이나 게임 오버 이후 실행되는 코루틴/애니메이션은 `WaitForSecondsRealtime`과 `Time.unscaledDeltaTime`을 사용해야 하며, 재시작 시 `Time.timeScale = 1f` 복원을 보장하세요.
