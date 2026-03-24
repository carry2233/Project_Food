using System.Collections;                     // 코루틴 사용
using System.Collections.Generic;            // List 사용
using UnityEngine;

[AddComponentMenu("Boss/Boss Entrance System")]  // 인스펙터 메뉴 경로
[RequireComponent(typeof(Collider2D))]           // 반드시 Collider2D 필요(트리거)
public class BossEntranceSystem : MonoBehaviour  // 보스 입장 시스템 본체
{
    // ─────────────────────────────────────────────
    // 내부 타입 정의
    // ─────────────────────────────────────────────

    public enum Axis                              // 포지션이 변할 축
    {
        X,                                        // X축
        Y,                                        // Y축
        Z                                         // Z축
    }

    public enum Direction                         // 이동 방향(+ / -)
    {
        Negative = -1,                            // 음수 방향
        Positive = 1                              // 양수 방향
    }

    [System.Serializable]
    public class MoveEntry                        // 개별 오브젝트 이동 설정 한 칸
    {
        [Header("대상 설정")]
        public Transform target;                  // ✅ 이동시킬 대상 Transform

        [Header("이동 축/방향")]
        public Axis axis = Axis.X;                // ✅ 어느 축으로 움직일지
        public Direction direction = Direction.Positive; // ✅ + 방향 또는 - 방향

        [Header("이동 속도 및 시간")]
        public float unitsPerSecond = 1f;         // ✅ 초당 이동 거리(유닛)
        public float duration = 1f;               // ✅ 이동이 진행되는 시간(초)
    }

    // ─────────────────────────────────────────────
    // 필드 (Variables)
    // ─────────────────────────────────────────────

    [Header("트리거 설정")]
    [SerializeField] private Collider2D triggerCollider;        // ✅ 플레이어를 감지할 트리거 콜라이더
    [SerializeField] private string playerTag = "Player";       // ✅ 플레이어 태그 문자열

    [Header("이동 설정 리스트")]
    [SerializeField] private List<MoveEntry> moveEntries        // ✅ 이동 설정 리스트
        = new List<MoveEntry>();

    [Header("오디오 설정")]
    [SerializeField] private AudioSource loopAudioToStop;       // ✅ 먼저 재생 중이던 루프 오디오(중단 & 초기화 대상)
    [SerializeField] private AudioSource changeLoopAudio;       // ✅ 이후 재생될 루프 오디오(변화 루프)
    [SerializeField] private int changeLoopStartFrameOffset = 0; // ✅ 변화 루프 시작 프레임 오프셋(양수: 나중, 음수: 일찍)

    [Header("프레임 환산용 설정")]
    [SerializeField] private float assumedFrameRate = 60f;      // ✅ 프레임을 초로 바꿀 때 기준이 되는 FPS 값

    [Header("보스 활성 상태 감지")]
    [SerializeField] private GameObject bossObject;             // ✅ 비활성화 여부를 감지할 보스 오브젝트
    [SerializeField] private bool disableTargetsOnBossInactive = true; // ✅ 보스 비활성화 시 타겟들을 비활성화할지 여부

    // 내부 상태
    private bool hasTriggered = false;                          // ✅ 이미 한 번 발동했는지 여부(중복 방지)
    private Coroutine sequenceCoroutine;                        // ✅ 진행 중인 시퀀스 코루틴 핸들

    private bool bossWasActive = true;                          // ✅ 이전 프레임의 보스 활성 상태 기록
    private bool targetsDisabledByBoss = false;                 // ✅ 보스 비활성화로 인해 타겟들을 이미 껐는지 여부

    // ─────────────────────────────────────────────
    // 유니티 콜백 (Unity Callbacks)
    // ─────────────────────────────────────────────

    private void Reset()                                        // ✅ 컴포넌트 리셋 시 기본 참조 자동 할당
    {
        Collider2D col = GetComponent<Collider2D>();            // ✅ 자기 Collider2D 가져오기
        triggerCollider = col;                                  // ✅ 트리거로 사용할 콜라이더 할당

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;                   // ✅ 반드시 트리거로 설정
        }
    }

    private void OnEnable()                                     // ✅ 활성화 시 내부 상태 초기화
    {
        // 보스가 할당되어 있다면, 현재 활성 상태를 기록
        if (bossObject != null)
        {
            bossWasActive = bossObject.activeInHierarchy;       // ✅ 보스의 현재 활성 상태 저장
        }
        else
        {
            bossWasActive = false;                              // ✅ 보스가 없으면 false로 초기화
        }

        targetsDisabledByBoss = false;                          // ✅ 타겟 비활성화 플래그 초기화
    }

    private void Update()                                       // ✅ 매 프레임 보스 비활성화 감지
    {
        if (!disableTargetsOnBossInactive)                      // ✅ 옵션이 꺼져 있으면 처리 안 함
            return;

        if (bossObject == null)                                 // ✅ 보스가 할당되지 않았으면 처리 안 함
            return;

        if (targetsDisabledByBoss)                              // ✅ 이미 한 번 타겟들을 껐다면 더 이상 처리 안 함
            return;

        bool isActiveNow = bossObject.activeInHierarchy;        // ✅ 현재 보스 활성 상태

        // "이전에는 활성(true)였다가 지금은 비활성(false)이 된 순간"을 감지
        if (bossWasActive && !isActiveNow)
        {
            DisableAllMoveTargets();                            // ✅ moveEntries의 모든 target 오브젝트 비활성화
            targetsDisabledByBoss = true;                       // ✅ 다시 호출되지 않도록 플래그 ON
        }

        bossWasActive = isActiveNow;                            // ✅ 다음 프레임을 위해 현재 상태 저장
    }

    private void OnTriggerEnter2D(Collider2D other)             // ✅ 트리거에 다른 콜라이더가 들어올 때 호출
    {
        if (hasTriggered)                                       // ✅ 이미 발동한 적이 있으면 무시
            return;

        // 플레이어 태그 검사 (자기 또는 부모에 Player 태그가 있는지 확인)
        Transform root = other.transform;                       // ✅ 접촉한 콜라이더의 트랜스폼
        if (!root.CompareTag(playerTag))                        // ✅ 바로 태그가 Player인지 먼저 확인
        {
            if (root.transform.parent == null ||                // ✅ 부모가 없거나
                !root.transform.parent.CompareTag(playerTag))   // ✅ 부모 태그도 Player가 아니면
            {
                return;                                         // ✅ 플레이어가 아니라고 판단하고 종료
            }
        }

        // 여기까지 왔으면 플레이어로 판정 → 시퀀스 시작
        hasTriggered = true;                                    // ✅ 중복 발동 방지 플래그 ON

        if (sequenceCoroutine != null)                          // ✅ 기존에 코루틴이 있다면 정지
        {
            StopCoroutine(sequenceCoroutine);
        }

        sequenceCoroutine = StartCoroutine(RunEntranceSequence()); // ✅ 보스 입장 시퀀스 시작
    }

    // ─────────────────────────────────────────────
    // 메인 시퀀스 코루틴
    // ─────────────────────────────────────────────

    private IEnumerator RunEntranceSequence()                   // ✅ 보스 입장 시 전체 흐름을 처리하는 코루틴
    {
        // 1) 기존 루프 오디오 중단 & 초기화
        if (loopAudioToStop != null)
        {
            loopAudioToStop.Stop();                             // ✅ 재생 중지
            loopAudioToStop.time = 0f;                          // ✅ 재생 위치 0으로 초기화
        }

        // 2) 변화 루프 오디오는 시작 전 일단 정지/초기화
        if (changeLoopAudio != null)
        {
            changeLoopAudio.Stop();                             // ✅ 혹시 재생 중이면 정지
            changeLoopAudio.time = 0f;                          // ✅ 재생 위치 초기화
        }

        // 이동 시간의 최댓값 계산 (전체 시퀀스 길이로 사용)
        float maxDuration = 0f;                                 // ✅ 이동 총 지속시간 중 최댓값
        for (int i = 0; i < moveEntries.Count; i++)
        {
            if (moveEntries[i] != null && moveEntries[i].duration > maxDuration)
            {
                maxDuration = moveEntries[i].duration;          // ✅ 가장 긴 duration 선택
            }
        }

        float elapsed = 0f;                                     // ✅ 경과 시간(초)
        int frameCount = 0;                                     // ✅ 경과 프레임 수
        bool changeLoopStarted = false;                         // ✅ 변화 루프 오디오가 이미 시작됐는지 여부

        // 3) 이동 + 오디오 시작 프레임 처리
        while (elapsed < maxDuration ||                        // ✅ 아직 이동이 남았거나
               (!changeLoopStarted && changeLoopAudio != null && changeLoopStartFrameOffset > 0)) // ✅ 오디오 시작을 기다리는 중이면
        {
            float dt = Time.deltaTime;                          // ✅ 이번 프레임 경과 시간
            elapsed += dt;                                      // ✅ 총 경과 시간 누적

            // 3-1) 각 MoveEntry에 따라 오브젝트 포지션 이동
            for (int i = 0; i < moveEntries.Count; i++)
            {
                MoveEntry entry = moveEntries[i];               // ✅ 현재 이동 설정 참조
                if (entry == null || entry.target == null)      // ✅ 대상이 없으면 스킵
                    continue;

                if (elapsed > entry.duration)                   // ✅ 이 엔트리의 이동 시간 끝났으면 스킵
                    continue;

                Vector3 dirVec = Vector3.zero;                  // ✅ 이동 방향 벡터
                switch (entry.axis)
                {
                    case Axis.X: dirVec = Vector3.right; break;    // ✅ X축 기준
                    case Axis.Y: dirVec = Vector3.up; break;       // ✅ Y축 기준
                    case Axis.Z: dirVec = Vector3.forward; break;  // ✅ Z축 기준
                }

                float sign = (float)entry.direction;            // ✅ +1 또는 -1
                Vector3 delta = dirVec * sign * entry.unitsPerSecond * dt; // ✅ 이번 프레임 이동량
                entry.target.position += delta;                 // ✅ 실제 포지션 적용
            }

            // 3-2) 변화 루프 오디오 시작 타이밍 처리
            if (changeLoopAudio != null && !changeLoopStarted)
            {
                if (changeLoopStartFrameOffset == 0)            // ✅ 0프레임이면 즉시 시작
                {
                    changeLoopAudio.Play();                     // ✅ 바로 재생 시작
                    changeLoopStarted = true;
                }
                else if (changeLoopStartFrameOffset > 0)        // ✅ 양수이면 그 프레임 이후에 시작
                {
                    if (frameCount >= changeLoopStartFrameOffset)
                    {
                        changeLoopAudio.Play();                 // ✅ 지정 프레임 도달 시 재생
                        changeLoopStarted = true;
                    }
                }
                else // changeLoopStartFrameOffset < 0          // ✅ 음수이면 '일찍 시작' → 클립을 앞에서부터 당겨서 시작
                {
                    float framesEarly = Mathf.Abs(changeLoopStartFrameOffset); // ✅ 몇 프레임 일찍 시작하는지
                    float timeOffset = 0f;                      // ✅ 오디오 클립 안에서의 시작 위치(초)

                    if (assumedFrameRate > 0.01f)
                    {
                        timeOffset = framesEarly / assumedFrameRate; // ✅ 프레임 → 초로 변환
                    }

                    // 클립 길이를 넘지 않도록 보정
                    if (changeLoopAudio.clip != null)
                    {
                        float clipLen = changeLoopAudio.clip.length;
                        if (timeOffset > clipLen)
                            timeOffset = clipLen;
                    }

                    changeLoopAudio.time = timeOffset;          // ✅ 클립 안에서의 시작 지점 설정
                    changeLoopAudio.Play();                     // ✅ 곧바로 재생 시작
                    changeLoopStarted = true;
                }
            }

            frameCount++;                                      // ✅ 프레임 카운트 증가
            yield return null;                                 // ✅ 다음 프레임까지 대기
        }

        // 시퀀스 종료 (추가로 처리할 것이 있으면 이 아래에 작성 가능)
        sequenceCoroutine = null;                              // ✅ 코루틴 핸들 정리
    }

    // ─────────────────────────────────────────────
    // 유틸리티 메서드
    // ─────────────────────────────────────────────

    private void DisableAllMoveTargets()                       // ✅ moveEntries의 모든 target 오브젝트를 비활성화하는 메서드
    {
        for (int i = 0; i < moveEntries.Count; i++)
        {
            MoveEntry entry = moveEntries[i];                   // ✅ 현재 엔트리 참조
            if (entry == null || entry.target == null)          // ✅ 대상이 없으면 스킵
                continue;

            entry.target.gameObject.SetActive(false);           // ✅ 대상 오브젝트 비활성화
        }
    }
}
