using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using HistoryCrash.Boss;
using HistoryCrash.CameraRig;
using HistoryCrash.Combat;
using HistoryCrash.Data;
using HistoryCrash.Enemy;
using HistoryCrash.Player;
using HistoryCrash.Stage;

namespace HistoryCrash.UI
{
    /// <summary>
    /// 획득 오브젝트 표시 UI. Player가 든 스태프(패턴 무기 A~E)와 오브(아누비스 지팡이 속성 코어)를 표시.
    /// - InkDraw(기본): 양피지 위에 잉크 윤곽이 펜으로 그려지듯 왼쪽→오른쪽으로 번지고, 이어서 채색이 스며듦.
    ///   바뀔 때는 이전 그림이 번지며 지워진 뒤 새로 그려짐.
    /// - CoinFlip: 아이콘이 Y축으로 한 바퀴 돌며 교체(이전 장비 슬롯 연출).
    /// </summary>
    public class WeaponUIController : MonoBehaviour
    {

        [Header("참조")]
        [SerializeField, FormerlySerializedAs("loadout")] private PlayerLoadout _loadout;
        [SerializeField, FormerlySerializedAs("database")] private WeaponDatabase _database;
        [SerializeField, FormerlySerializedAs("weaponSlot"), Tooltip("패턴 무기(주운 스태프 A~E)")] private WeaponUISlot _weaponSlot = new WeaponUISlot();
        [SerializeField, FormerlySerializedAs("coreSlot"), Tooltip("아누비스 지팡이 + 속성 코어(주운 오브)")] private WeaponUISlot _coreSlot = new WeaponUISlot();

        [Header("연출")]
        [SerializeField, FormerlySerializedAs("style")] private WeaponUIStyle _style = WeaponUIStyle.InkDraw;
        [SerializeField, FormerlySerializedAs("inkColor"), Tooltip("잉크 색(양피지 위 먹선)")] private Color _inkColor = new Color(0.16f, 0.09f, 0.04f, 0.92f);
        [SerializeField, FormerlySerializedAs("paintTint"), Tooltip("양피지에 스민 듯한 채색 톤")] private Color _paintTint = new Color(1f, 0.93f, 0.80f, 0.93f);
        [SerializeField, FormerlySerializedAs("eraseTime")] private float _eraseTime = 0.22f;
        [SerializeField, FormerlySerializedAs("inkTime")] private float _inkTime = 0.55f;
        [SerializeField, FormerlySerializedAs("paintTime")] private float _paintTime = 0.45f;
        [SerializeField, FormerlySerializedAs("inkRemain"), Range(0f, 1f)] private float _inkRemain = 0.18f;

        [Header("CoinFlip")]
        [SerializeField, FormerlySerializedAs("flipTime")] private float _flipTime = 0.42f;
        [SerializeField, FormerlySerializedAs("peakScale")] private float _peakScale = 1.35f;
        [SerializeField, FormerlySerializedAs("landPunch")] private float _landPunch = 0.12f;
        [SerializeField, FormerlySerializedAs("landTime")] private float _landTime = 0.18f;

        private int _lastStaff = int.MinValue; private bool _lastHasOrb; private Element _lastOrb;

        private void Start()
        {
            if (!_loadout) _loadout = FindFirstObjectByType<PlayerLoadout>();
            Init(_weaponSlot); Init(_coreSlot);
        }

        private void Init(WeaponUISlot _s)
        {
            if (!_s.Icon) return;
            if (!_s.Root) _s.Root = _s.Icon.rectTransform;
            _s.Icon.preserveAspect = true; _s.Icon.raycastTarget = false; _s.Icon.enabled = false;
            if (_s.Ink)
            {
                _s.Ink.preserveAspect = true; _s.Ink.raycastTarget = false; _s.Ink.enabled = false;
                _s.Ink.type = Image.Type.Filled; _s.Ink.fillMethod = Image.FillMethod.Horizontal; _s.Ink.fillOrigin = (int)Image.OriginHorizontal.Left;
            }
            if (_s.Caption) _s.Caption.text = "";
        }

        // Player 상태를 보고 바뀐 슬롯만 연출(PlayerLoadout 수정 없이 연결)
        private void Update()
        {
            if (!_loadout || !_database) return;
            if (_loadout.HeldStaff != _lastStaff)
            {
                _lastStaff = _loadout.HeldStaff;
                SetWeapon(_weaponSlot, _database.GetPattern(_lastStaff));
            }
            if (_loadout.HasOrb != _lastHasOrb || (_loadout.HasOrb && _loadout.HeldOrb != _lastOrb))
            {
                _lastHasOrb = _loadout.HasOrb; _lastOrb = _loadout.HeldOrb;
                SetWeapon(_coreSlot, _lastHasOrb ? _database.GetCore(_lastOrb) : null);
            }
        }

        public void ShowPattern(int _staffIndex) { SetWeapon(_weaponSlot, _database ? _database.GetPattern(_staffIndex) : null); }
        public void ShowCore(Element _e) { SetWeapon(_coreSlot, _database ? _database.GetCore(_e) : null); }

        public void SetWeapon(WeaponUISlot _s, WeaponData _data)
        {
            if (_s.Icon == null) return;
            if (_data != null && _data == _s.Current) return;
            var _prev = _s.Current; _s.Current = _data;
            _s.Running?.Kill();
            _s.Root.localRotation = Quaternion.identity; _s.Root.localScale = Vector3.one;
            _s.Running = _style == WeaponUIStyle.InkDraw ? InkDraw(_s, _prev, _data) : Flip(_s, _data);
        }

        // ---------- 양피지 잉크 그리기(DOTween, 게임이 멈춰도 흐르는 시간) ----------
        private Sequence InkDraw(WeaponUISlot _s, WeaponData _prev, WeaponData _data)
        {
            var _seq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            // 1) 이전 그림이 번지며 지워짐(살짝 커지며 흐려짐)
            if (_s.Icon.enabled)
            {
                _seq.Insert(0f, _s.Icon.DOFade(0f, _eraseTime).SetEase(Ease.Linear));
                if (_s.Ink) _seq.Insert(0f, _s.Ink.DOFade(0f, _eraseTime).SetEase(Ease.Linear));
                if (_s.Caption) _seq.Insert(0f, _s.Caption.DOFade(0f, _eraseTime).SetEase(Ease.Linear));
                _seq.Insert(0f, _s.Root.DOScale(1.08f, _eraseTime).SetEase(Ease.Linear));
            }
            _seq.AppendCallback(() =>
            {
                _s.Root.localScale = Vector3.one;
                _s.Icon.enabled = false; if (_s.Ink) _s.Ink.enabled = false;
                if (_s.Caption) _s.Caption.text = "";
            });
            if (!_data || !_data.WeaponIcon) return _seq;

            // 2) 잉크 윤곽이 왼쪽→오른쪽으로 그려짐(펜 속도: 처음 빠르고 끝에서 느려짐) + 펜 떨림
            if (_s.Ink)
            {
                _seq.AppendCallback(() => { _s.Icon.sprite = _data.WeaponIcon; _s.Ink.sprite = _data.WeaponIcon; _s.Ink.enabled = true; _s.Ink.color = _inkColor; _s.Ink.fillAmount = 0f; });
                float _at = _seq.Duration(false);
                _seq.Insert(_at, _s.Ink.DOFillAmount(1f, _inkTime).SetEase(Ease.OutQuad));
                _seq.Insert(_at, DOVirtual.Float(0f, 1f, _inkTime, u => _s.Root.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(u * Mathf.PI * 3f) * 1.2f * (1f - u))).SetEase(Ease.Linear));
                _seq.AppendCallback(() => _s.Root.localRotation = Quaternion.identity);
            }
            else _seq.AppendCallback(() => _s.Icon.sprite = _data.WeaponIcon);

            // 3) 채색이 스며듦 + 이름 글씨
            _seq.AppendCallback(() =>
            {
                _s.Icon.enabled = true; _s.Icon.color = new Color(_paintTint.r, _paintTint.g, _paintTint.b, 0f);
                if (_s.Caption) { _s.Caption.text = _data.DisplayName; var _c = _s.Caption.color; _c.a = 0f; _s.Caption.color = _c; }
            });
            float _p0 = _seq.Duration(false);
            _seq.Insert(_p0, _s.Icon.DOFade(_paintTint.a, _paintTime).SetEase(Ease.InOutSine));
            if (_s.Ink) _seq.Insert(_p0, _s.Ink.DOFade(_inkRemain, _paintTime).SetEase(Ease.InOutSine));
            if (_s.Caption) _seq.Insert(_p0, _s.Caption.DOFade(1f, _paintTime).SetEase(Ease.InOutSine));
            return _seq;
        }

        // ---------- 동전 뒤집기(이전 연출) ----------
        private Sequence Flip(WeaponUISlot _s, WeaponData _data)
        {
            Color _sig = _data ? _data.SignatureColor : Color.white;
            float _quarter = _flipTime * 0.25f, _rest = _flipTime - _quarter;
            var _seq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            _seq.Insert(0f, _s.Root.DOLocalRotate(new Vector3(0f, 90f, 0f), _quarter).SetEase(Ease.InQuad));
            _seq.Insert(0f, _s.Root.DOScale(_peakScale, _quarter).SetEase(Ease.OutQuad));
            _seq.AppendCallback(() =>
            {
                _s.Icon.sprite = _data ? _data.WeaponIcon : null;
                _s.Icon.enabled = _data && _data.WeaponIcon; _s.Icon.color = Color.white;
                if (_s.Ink) _s.Ink.enabled = false;
                if (_s.Glow) _s.Glow.color = _data ? new Color(_sig.r, _sig.g, _sig.b, 0.9f) : new Color(1, 1, 1, 0.25f);
            });
            _seq.Insert(_quarter, _s.Root.DOLocalRotate(new Vector3(0f, 360f, 0f), _rest, RotateMode.FastBeyond360).SetEase(Ease.OutCubic));
            _seq.Insert(_quarter, _s.Root.DOScale(1f, _rest).SetEase(Ease.OutBack, 2.2f));
            _seq.AppendCallback(() => _s.Root.localRotation = Quaternion.identity);
            _seq.Append(_s.Root.DOPunchScale(Vector3.one * _landPunch, _landTime, 2, 0.5f));
            _seq.OnComplete(() => _s.Root.localScale = Vector3.one);
            return _seq;
        }

        private static float OutBack(float _u, float _s) { _u -= 1f; return _u * _u * ((_s + 1f) * _u + _s) + 1f; }
    }
}
