using UnityEngine;
using HistoryCrash.Boss;
using HistoryCrash.CameraRig;
using HistoryCrash.Combat;
using HistoryCrash.Enemy;
using HistoryCrash.Player;
using HistoryCrash.Stage;
using HistoryCrash.UI;

namespace HistoryCrash.Data
{
    public static class ElementInfo
    {
        public const int COUNT = 3;   // 등장하는 속성 수. 풀은 파라오 공격 예고(연두)와 색이 겹치고 난이도를 낮추려고 뺌(값·데이터는 남김)
        private static readonly string[] _names = { "Fire", "Water", "Electric", "Grass" };
        private static readonly string[] _namesKo = { "불", "물", "전기", "풀" };
        // Blender EnergyShell 발광색과 같은 계열. 불은 전기(노랑)와 헷갈리지 않게 주황기를 뺀 진한 빨강
        private static readonly Color[] _colors =
        {
            new Color(0.90f, 0.04f, 0.02f),
            new Color(0.02f, 0.32f, 1.00f),
            new Color(1.00f, 0.78f, 0.04f),
            new Color(0.12f, 0.85f, 0.08f),
        };

        public static Color Color(Element _e) { return _colors[(int)_e]; }
        public static string Name(Element _e) { return _names[(int)_e]; }
        public static string NameKo(Element _e) { return _namesKo[(int)_e]; }
        public static Element Random() { return (Element)UnityEngine.Random.Range(0, COUNT); }
    }
}
