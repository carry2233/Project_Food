using UnityEngine;

[AddComponentMenu("Audio/SFX Manager")]
[DisallowMultipleComponent]
public class SfxManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────────
    // 변수 (Variables)
    // ─────────────────────────────────────────────────────────────────────────────

    public static SfxManager Instance { get; private set; } // ✅ 전역에서 접근할 싱글톤 인스턴스

    [Header("기본 설정")]
    [SerializeField] private AudioSource audioSource;        // ✅ 효과음을 재생할 AudioSource
    [Range(0f, 1f)]
    [SerializeField] private float masterVolume = 1f;        // ✅ 전체 효과음 볼륨(0~1)

    // ─────────────────────────────────────────────────────────────────────────────
    // 유니티 콜백 (Unity Callbacks)
    // ─────────────────────────────────────────────────────────────────────────────

    private void Awake()                                     // ✅ 싱글톤 및 AudioSource 초기화
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);                             // ✅ 중복 생성 방지
            return;
        }

        Instance = this;

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();       // ✅ 같은 오브젝트에서 AudioSource 찾기
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>(); // ✅ 없으면 자동 추가
            }
        }

        audioSource.playOnAwake = false;                     // ✅ 시작 시 자동 재생 비활성화
        audioSource.loop = false;                            // ✅ 기본적으로 루프 사용 안 함
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 메서드 (Methods)
    // ─────────────────────────────────────────────────────────────────────────────

    public void PlaySfx(AudioClip clip)                      // ✅ 지정한 클립을 기본 볼륨으로 재생
    {
        if (clip == null || audioSource == null)
            return;

        audioSource.PlayOneShot(clip, masterVolume);
    }

    public void PlaySfx(AudioClip clip, float volumeScale)   // ✅ 추가 볼륨 스케일을 곱해서 재생
    {
        if (clip == null || audioSource == null)
            return;

        float finalVolume = Mathf.Clamp01(masterVolume * volumeScale);
        audioSource.PlayOneShot(clip, finalVolume);
    }

    public void SetMasterVolume(float value)                 // ✅ 전체 효과음 볼륨 설정(0~1)
    {
        masterVolume = Mathf.Clamp01(value);
    }
}
