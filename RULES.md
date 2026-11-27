# Unity Project AI Rules
- 사용자가 명시적으로 요청하지 않는 한 `.unity`, `.prefab`, `.asset` 파일의 직렬화된 인스펙터 값(Transform, Scale, Speed, Position, 참조 연결)을 절대 수정하거나 초기화하지 말 것.
- 기본 작업은 `Assets/Scripts/` 내의 `.cs` 파일 수정으로 한정할 것.