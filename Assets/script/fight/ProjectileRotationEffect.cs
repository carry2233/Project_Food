using UnityEngine;

[AddComponentMenu("Visual/Projectile Rotation Effect (투사체회전이펙트)")]
[DisallowMultipleComponent]
public class ProjectileRotationEffect : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────────
    // 변수 (Variables)
    // ─────────────────────────────────────────────────────────────────────────────

    [Header("회전 설정")]
    [SerializeField] private Transform rotateTarget;                 // ✅ 회전시킬 대상 Transform
    [SerializeField] private float rotationSpeed = 360f;             // ✅ 초당 회전 속도(도/초)
    [SerializeField] private int rotationDirection = 1;              // ✅ 회전 방향(+1 / -1)

    [Header("스프라이트1 깜빡임")]
    [SerializeField] private SpriteRenderer blinkRenderer;           // ✅ on/off 깜빡임할 SpriteRenderer
    [SerializeField, Min(0.01f)]
    private float blinkInterval = 0.1f;                             // ✅ on/off 전환 주기(초)

    [Header("스프라이트2 알파 토글")]
    [SerializeField] private SpriteRenderer alphaRenderer;          // ✅ 알파를 토글할 SpriteRenderer
    [SerializeField, Range(0f, 1f)] private float alphaValue1 = 0.3f; // ✅ 알파 값1
    [SerializeField, Range(0f, 1f)] private float alphaValue2 = 1.0f; // ✅ 알파 값2
    [SerializeField, Min(0.01f)]
    private float alphaToggleInterval = 0.1f;                       // ✅ 알파 전환 주기(초)

    // 내부 상태
    private float blinkTimer;                                       // ✅ 스프라이트1 깜빡임 타이머
    private float alphaTimer;                                       // ✅ 스프라이트2 알파 토글 타이머
    private bool useAlpha1 = true;                                  // ✅ 현재 알파가 value1인지 여부

    // ─────────────────────────────────────────────────────────────────────────────
    // 유니티 콜백 (Unity Callbacks)
    // ─────────────────────────────────────────────────────────────────────────────

    private void OnEnable() // ✅ 활성화 시 타이머 초기화
    {
        blinkTimer = 0f;
        alphaTimer = 0f;
        useAlpha1 = true;

        if (alphaRenderer != null)
            SetAlpha(alphaRenderer, alphaValue1);                   // 시작 알파 적용
    }

    private void Update() // ✅ 회전/깜빡임/알파 토글 처리
    {
        if (!gameObject.activeInHierarchy)
            return;

        float dt = Time.deltaTime;

        // 1) 회전 처리
        if (rotateTarget != null && Mathf.Abs(rotationSpeed) > 0.01f)
        {
            float dir = (rotationDirection >= 0) ? 1f : -1f;
            rotateTarget.Rotate(0f, 0f, dir * rotationSpeed * dt);
        }

        // 2) 스프라이트1 깜빡임
        if (blinkRenderer != null)
        {
            blinkTimer += dt;
            if (blinkTimer >= blinkInterval)
            {
                blinkTimer -= blinkInterval;
                blinkRenderer.enabled = !blinkRenderer.enabled;      // ✅ on/off 토글
            }
        }

        // 3) 스프라이트2 알파 토글
        if (alphaRenderer != null)
        {
            alphaTimer += dt;
            if (alphaTimer >= alphaToggleInterval)
            {
                alphaTimer -= alphaToggleInterval;
                useAlpha1 = !useAlpha1;
                float targetAlpha = useAlpha1 ? alphaValue1 : alphaValue2;
                SetAlpha(alphaRenderer, targetAlpha);               // ✅ 알파 값1 ↔ 값2 토글
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 메서드 (Methods)
    // ─────────────────────────────────────────────────────────────────────────────

    private void SetAlpha(SpriteRenderer sr, float a) // 🔧 SpriteRenderer 알파 설정
    {
        Color c = sr.color;
        c.a = Mathf.Clamp01(a);
        sr.color = c;
    }
}
