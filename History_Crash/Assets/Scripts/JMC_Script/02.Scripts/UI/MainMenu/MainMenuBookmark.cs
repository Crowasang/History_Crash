using UnityEngine;
using UnityEngine.EventSystems;

namespace HistoryCrash.UI
{
    /// <summary>
    /// 메인메뉴 도감의 천 책갈피(뒷장에 끼워져 책 옆면 밖으로 나온 것). 누르면 Clicked 실행.
    /// 책갈피는 책의 일부라 움직이거나 깜빡이는 연출 없이 그대로 둔다.
    /// 우측 = 건너뛰기·다음 장·모험 시작, 좌측 = 이전 장. MainMenuController가 실행 중에 스스로 붙임.
    /// </summary>
    public class MainMenuBookmark : MonoBehaviour, IPointerClickHandler
    {
        public System.Action Clicked;
        public float Dir = 1f;   // 우측 +1, 좌측 -1(지금은 구분용)

        public void OnPointerClick(PointerEventData _e) { Clicked?.Invoke(); }
    }
}
