using UnityEngine;

namespace HistoryCrash.UI
{
    /// <summary>
    /// 게임 언어(설정창 Language에서 고름). PlayerPrefs "HC_Language"에 저장되고, 기본은 한국어.
    /// 지금은 아이템 줍기 안내 문구에 쓰인다. 문구를 늘릴 때는 T(한국어, 영어)로 고르면 된다.
    /// </summary>
    public static class GameLanguage
    {
        public enum Lang { Korean = 0, English = 1 }
        private const string KEY = "HC_Language";
        private static bool _loaded; private static Lang _current;

        public static event System.Action Changed;

        public static Lang Current
        {
            get { if (!_loaded) { _current = (Lang)PlayerPrefs.GetInt(KEY, (int)Lang.Korean); _loaded = true; } return _current; }
            set
            {
                if (_loaded && _current == value) return;
                _current = value; _loaded = true;
                PlayerPrefs.SetInt(KEY, (int)value);
                Changed?.Invoke();
            }
        }

        public static bool IsKorean => Current == Lang.Korean;

        /// <summary>지금 언어에 맞는 문구</summary>
        public static string T(string _ko, string _en) { return IsKorean ? _ko : _en; }
    }
}
