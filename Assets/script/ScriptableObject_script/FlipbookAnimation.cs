using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(
    fileName = "FlipbookAnimation",
    menuName = "Animations/Flipbook Animation",
    order = 0
)]
public class FlipbookAnimation : ScriptableObject
{
    [System.Serializable]
    public struct Frame
    {
        public Sprite sprite;                  // ✅ 이 프레임에 표시될 스프라이트 이미지
        public bool overrideInterval;          // ✅ 이 프레임만 별도의 주기를 사용할지 여부
        [Min(0.001f)]
        public float customInterval;           // ✅ overrideInterval이 true일 때 사용할 프레임 전환 주기(초)
    }

    [Header("플립북 프레임 목록")]
    public List<Frame> frames = new List<Frame>();   // ✅ 재생 순서대로 프레임을 등록

    /// <summary>
    /// 지정 인덱스의 프레임을 안전하게 가져오기
    /// </summary>
    public bool TryGetFrame(int index, out Frame frame)             // ✅ 프레임 안전 취득 메서드
    {
        if (frames != null && frames.Count > 0)
        {
            int i = Mathf.Abs(index) % frames.Count;
            frame = frames[i];
            return true;
        }
        frame = default;
        return false;
    }

    /// <summary>
    /// 프레임 수 반환(0일 수 있음)
    /// </summary>
    public int FrameCount()                                         // ✅ 프레임 수 조회 메서드
    {
        return (frames == null) ? 0 : frames.Count;
    }
}
