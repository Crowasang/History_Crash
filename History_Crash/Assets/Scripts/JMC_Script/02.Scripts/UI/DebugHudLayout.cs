using UnityEngine;

namespace HistoryCrash.UI
{
    /// <summary>
    /// 화면 왼쪽 위 디버그 글자(OnGUI)의 위치 기준.
    /// 좌측 상단에는 플레이어 HP 나침반(1920×1080 기준 330px)이 있으므로, 디버그 글자는 그 아래부터 그린다.
    /// 나침반은 화면 크기에 맞춰 커지고 작아지므로 내릴 거리도 화면 높이로 계산한다.
    /// </summary>
    public static class DebugHudLayout
    {
        /// <summary>디버그 글자를 시작할 높이(px): 나침반 높이 + 여백</summary>
        public static float Top => Screen.height * (340f / 1080f);
    }
}
