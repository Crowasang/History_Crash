using UnityEngine;

namespace HistoryCrash.Sound
{
    /// <summary>
    /// 게임 전체 소리의 기준 크기. 설정창 Master·Music·Effects가 모두 100일 때 실제로는 BASE만큼만 나온다.
    /// 메인메뉴·컷씬·배경음·효과음 모두에 똑같이 곱해지므로 소리끼리의 균형은 바뀌지 않는다(설정창 숫자도 그대로).
    /// </summary>
    public static class GameVolume
    {
        /// <summary>1 = 예전 크기(100), 0.6 = 60</summary>
        public const float BASE = 0.6f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply() { AudioListener.volume = BASE; }
    }
}
