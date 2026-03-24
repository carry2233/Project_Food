using UnityEngine;                         // ✅ 유니티 기본 네임스페이스
using UnityEngine.SceneManagement;         // ✅ 씬 이동을 위한 네임스페이스

[AddComponentMenu("Scene/Contact Scene Transition")]           // ✅ 인스펙터 상단 메뉴 경로
[RequireComponent(typeof(Collider2D))]                         // ✅ 반드시 Collider2D가 필요함
public class ContactSceneTransition : MonoBehaviour            // ✅ "접촉씬이동" 기능 스크립트
{
    [Header("트리거 콜라이더 설정")]
    [SerializeField] private Collider2D triggerCollider;       // ✅ 플레이어를 감지할 2D 트리거 콜라이더
    [SerializeField] private string playerTag = "Player";      // ✅ 플레이어로 인식할 태그 이름

    [Header("이동할 씬 설정")]
    [SerializeField] private string sceneToLoad;               // ✅ 접촉 시 이동할 씬 이름 (Build Settings에 등록된 이름)

    [Header("중복 실행 여부")]
    [SerializeField] private bool loadOnlyOnce = true;         // ✅ 한 번만 씬 이동을 허용할지 여부
    private bool hasLoaded = false;                            // ✅ 이미 씬 이동을 실행했는지 여부

    private void Reset()                                       // ✅ 컴포넌트 추가/리셋 시 기본 설정
    {
        triggerCollider = GetComponent<Collider2D>();          // ✅ 자기 자신에 붙은 Collider2D를 가져옴

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;                  // ✅ 반드시 트리거로 사용
        }
    }

    private void OnTriggerEnter2D(Collider2D other)            // ✅ 다른 2D 콜라이더가 트리거에 들어왔을 때 호출
    {
        // 이미 한 번 씬 이동을 했다면 중복 실행 방지
        if (loadOnlyOnce && hasLoaded)
            return;

        // 플레이어 태그 검사 (자신 또는 부모 오브젝트에 태그가 있는 경우까지 체크)
        Transform t = other.transform;                         // ✅ 충돌한 오브젝트의 트랜스폼
        bool isPlayer = t.CompareTag(playerTag);               // ✅ 자기 자신 태그 체크

        if (!isPlayer && t.parent != null)                     // ✅ 자기 자신이 아니면 부모 태그도 확인
        {
            isPlayer = t.parent.CompareTag(playerTag);
        }

        if (!isPlayer)                                         // ✅ 플레이어가 아니면 무시
            return;

        // 씬 이름이 비어 있으면 이동하지 않음
        if (string.IsNullOrEmpty(sceneToLoad))
        {
            Debug.LogWarning("[ContactSceneTransition] 이동할 씬 이름이 비어 있습니다.", this);
            return;
        }

        // 씬 이동 실행
        LoadScene();                                           // ✅ 씬 이동 메서드 호출
    }

    private void LoadScene()                                   // ✅ 실제 씬 이동을 수행하는 메서드
    {
        if (loadOnlyOnce)                                      // ✅ 한 번만 실행하도록 설정된 경우
        {
            hasLoaded = true;                                  // ✅ 재실행 방지 플래그 ON
        }

        SceneManager.LoadScene(sceneToLoad);                   // ✅ 지정된 씬 이름으로 이동
    }
}
