using System;
using UnityEngine;

namespace HistoryCrash.UI
{
    /// <summary>
    /// 메인메뉴 크레딧 한 줄: 역할(한국어/영어) + 그 역할을 맡은 사람들 이름.
    /// </summary>
    [Serializable]
    public class MainMenuCreditEntry
    {
        [SerializeField] private string _roleKo;
        [SerializeField] private string _roleEn;
        [SerializeField] private string[] _names;

        public string Role => GameLanguage.T(_roleKo, _roleEn);
        public string[] Names => _names;

        public MainMenuCreditEntry() { }

        public MainMenuCreditEntry(string _ko, string _en, params string[] _people)
        {
            _roleKo = _ko; _roleEn = _en; _names = _people;
        }
    }
}
