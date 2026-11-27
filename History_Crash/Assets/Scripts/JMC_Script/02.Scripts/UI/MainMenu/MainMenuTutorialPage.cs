using System;
using UnityEngine;

namespace HistoryCrash.UI
{
    /// <summary>
    /// 메인메뉴 도감의 튜토리얼 한 장: 왼쪽 페이지 그림 + 오른쪽 페이지 제목·설명(한국어/영어).
    /// 그림은 Resources 경로로 불러온다(Assets/Resources/UI/Tutorial/).
    /// </summary>
    [Serializable]
    public class MainMenuTutorialPage
    {
        [SerializeField, Tooltip("Resources 기준 그림 경로(확장자 없이). 예: UI/Tutorial/01_BossPattern")] private string _imagePath;
        [SerializeField] private string _titleKo;
        [SerializeField] private string _titleEn;
        [SerializeField, TextArea(3, 8)] private string _bodyKo;
        [SerializeField, TextArea(3, 8)] private string _bodyEn;

        public string ImagePath => _imagePath;
        public string Title => GameLanguage.T(_titleKo, _titleEn);
        public string Body => GameLanguage.T(_bodyKo, _bodyEn);

        public MainMenuTutorialPage() { }

        public MainMenuTutorialPage(string _image, string _tKo, string _tEn, string _bKo, string _bEn)
        {
            _imagePath = _image; _titleKo = _tKo; _titleEn = _tEn; _bodyKo = _bKo; _bodyEn = _bEn;
        }
    }
}
