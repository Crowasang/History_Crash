using UnityEngine;
using UnityEngine.EventSystems;

namespace HistoryCrash.UI
{
    /// <summary>
    /// 메인 메뉴 한 줄(Start·Continue·Settings·Quit). 마우스를 올리면 선택, 누르면 실행을 MainMenuController에 알린다.
    /// MainMenuController가 실행 중에 스스로 붙이므로 직접 붙일 필요는 없다.
    /// </summary>
    public class MainMenuItem : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        public MainMenuController Owner;
        public int Index;

        public void OnPointerEnter(PointerEventData _e) { if (Owner) Owner.Select(Index); }
        public void OnPointerClick(PointerEventData _e) { if (Owner) Owner.Submit(Index); }
    }
}
