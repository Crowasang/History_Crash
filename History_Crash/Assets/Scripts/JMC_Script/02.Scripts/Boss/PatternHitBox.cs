using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using HistoryCrash.CameraRig;
using HistoryCrash.Combat;
using HistoryCrash.Data;
using HistoryCrash.Enemy;
using HistoryCrash.Player;
using HistoryCrash.Stage;
using HistoryCrash.UI;

namespace HistoryCrash.Boss
{
            // ---------- 메시 콜라이더 기준 피격 ----------
            // 각 투사체 메시에 MeshCollider(Ignore Raycast 레이어)를 달고, 매 프레임 Player 구(모델 반지름)와 겹치는지 검사한다.
            // 스치기만 해도 맞는다. 데미지 후 PlayerHealth 무적(1초) 동안은 추가 피해 없음.
            public class PatternHitBox { 
            private MeshCollider _col;
            public MeshCollider Col { get => _col; set => _col = value; }

            private Transform _t;
            public Transform T { get => _t; set => _t = value; }

            private int _pattern;
            private int _slot;
            public int Pattern { get => _pattern; set => _pattern = value; }
            public int Slot { get => _slot; set => _slot = value; }

            private string _label;
            public string Label { get => _label; set => _label = value; }
    }
}
