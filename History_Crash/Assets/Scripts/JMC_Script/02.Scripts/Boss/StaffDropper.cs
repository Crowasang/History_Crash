using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using HistoryCrash.CameraRig;
using HistoryCrash.Combat;
using HistoryCrash.Data;
using HistoryCrash.Enemy;
using HistoryCrash.Player;
using HistoryCrash.Stage;
using HistoryCrash.UI;

namespace HistoryCrash.Boss
{
    /// <summary>
    /// 보스 패턴이 끝난 자리에 스태프를 떨어뜨리고, Player가 E키로 줍게 한다.
    /// - 하늘에서 빠르게 떨어져 타일 정중앙에 수직으로 박힌다(다른 스태프가 있는 칸이면 가장 가까운 빈 칸).
    /// - 박힌 스태프는 반투명. 어떤 패턴인지 알 수 있게 패턴 오브젝트의 작은 복제가 위에서 떠다니며 돈다.
    /// - 박힌 뒤 despawnTime초가 지나면 서서히 사라진다.
    /// - Player 칸에서 pickupRange칸 안의 스태프는 E키로 획득(가장 가까운 것).
    /// </summary>
    public class StaffDropper : MonoBehaviour
    {
        [SerializeField, FormerlySerializedAs("grid")] private TileGrid _grid;
        [SerializeField, FormerlySerializedAs("staffMesh"), Tooltip("스태프 메시(머리 장식이 +Y)")] private Mesh _staffMesh;
        [SerializeField, FormerlySerializedAs("staffMaterial")] private Material _staffMaterial;

        [Header("모양")]
        [SerializeField, FormerlySerializedAs("staffLength"), Tooltip("스태프 월드 길이(m)")] private float _staffLength = 0.9f;
        [SerializeField, FormerlySerializedAs("embedDepth"), Tooltip("타일에 박히는 깊이(m)")] private float _embedDepth = 0.12f;
        [SerializeField, FormerlySerializedAs("staffAlpha"), Range(0f, 1f)] private float _staffAlpha = 0.8f;

        [Header("박힘 연출")]
        [SerializeField, FormerlySerializedAs("impactSink"), Tooltip("박히는 순간 더 깊이 들어갔다가 튕겨 올라오는 깊이(m)")] private float _impactSink = 0.12f;
        [SerializeField, FormerlySerializedAs("impactReboundTime")] private float _impactReboundTime = 0.22f;
        [SerializeField, FormerlySerializedAs("impactSparkColor")] private Color _impactSparkColor = new Color(1f, 0.78f, 0.35f);
        [SerializeField, FormerlySerializedAs("impactDustColor")] private Color _impactDustColor = new Color(0.32f, 0.28f, 0.22f, 0.55f);

        [Header("낙하")]
        [SerializeField, FormerlySerializedAs("dropHeight")] private float _dropHeight = 12f;
        [SerializeField, FormerlySerializedAs("startSpeed")] private float _startSpeed = 18f;
        [SerializeField, FormerlySerializedAs("gravity")] private float _gravity = 90f;
        [SerializeField, FormerlySerializedAs("wobbleTime"), Tooltip("박힐 때 살짝 흔들리는 시간(초)")] private float _wobbleTime = 0.35f;
        [SerializeField, FormerlySerializedAs("wobbleAngle")] private float _wobbleAngle = 10f;

        [Header("패턴 표시(스태프 위 작은 패턴 오브젝트)")]
        [SerializeField, FormerlySerializedAs("iconSize"), Tooltip("가장 긴 변 기준 크기(m)")] private float _iconSize = 0.32f;
        [SerializeField, FormerlySerializedAs("iconGap"), Tooltip("스태프 머리 위로 띄우는 높이(m)")] private float _iconGap = 0.28f;
        [SerializeField, FormerlySerializedAs("iconBob")] private float _iconBob = 0.06f;
        [SerializeField, FormerlySerializedAs("iconBobSpeed")] private float _iconBobSpeed = 2.2f;
        [SerializeField, FormerlySerializedAs("iconSpin")] private float _iconSpin = 70f;

        [Header("유지·획득")]
        [SerializeField, FormerlySerializedAs("despawnTime"), Tooltip("박힌 뒤 사라지기까지(초)")] private float _despawnTime = 10f;
        [SerializeField, FormerlySerializedAs("fadeOutTime"), Tooltip("사라질 때 흐려지는 시간(초)")] private float _fadeOutTime = 1f;
        // 획득은 PlayerLoadout(E키)가 PickupItem을 통해 처리한다

        private static readonly string[] _names = { "A", "B", "C", "D", "E" };
        // 패턴 오브젝트를 세워서 보이게 하는 회전(메시 기준). B·D는 장면 배치와 같은 회전
        private static readonly Vector3[] _iconEuler = { new Vector3(0, 0, 90), new Vector3(90, 0, 0), new Vector3(90, 0, 0), new Vector3(90, 0, 0), Vector3.zero };
        private readonly List<StaffDropEntry> _drops = new List<StaffDropEntry>();
        private Material _fadedMat;

        public int Count { get { return _drops.Count; } }

        /// <summary>pattern(0=A~4=E)의 스태프를 world 위치 칸 중앙(차 있으면 가장 가까운 빈 칸)에 떨어뜨린다.</summary>
        public void Drop(int _pattern, Vector3 _world, Transform _iconSource)
        {
            if (!_staffMesh || _grid == null) return;
            var _cell = FreeCell(_grid.WorldToCell(_world));
            Vector3 _center = _grid.CellCenter(_cell, _grid.FloorTopY);

            var _root = new GameObject("Staff_Drop_" + _names[Mathf.Clamp(_pattern, 0, 4)]);
            _root.transform.position = _center;
            var _staff = new GameObject("Staff");
            _staff.transform.SetParent(_root.transform, false);
            _staff.AddComponent<MeshFilter>().sharedMesh = _staffMesh;
            var _mr = _staff.AddComponent<MeshRenderer>();
            var _mat = new Material(FadedMaterial());   // 개별 페이드아웃을 위해 스태프마다 복사
            _mr.sharedMaterial = _mat;
            _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            float _scale = _staffLength / Mathf.Max(_staffMesh.bounds.size.y, 1e-3f);
            _staff.transform.localScale = Vector3.one * _scale;
            float _bottom = _staffMesh.bounds.min.y * _scale;
            _staff.transform.localPosition = new Vector3(0, -_bottom - _embedDepth, 0);
            _staff.transform.localRotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

            Transform _icon = MakeIcon(_iconSource, _root.transform, _pattern);
            var _item = _root.AddComponent<PickupItem>();
            _item.Kind = PickupKind.Staff; _item.Value = _pattern; _item.Cell = _cell; _item.Ready = false;
            StaffDropEntry _d = new StaffDropEntry { Root = _root, Pattern = _pattern, Cell = _cell, Mat = _mat, IconRenderers = _icon ? _icon.GetComponentsInChildren<Renderer>() : new Renderer[0] };
            _drops.Add(_d);
            StartCoroutine(Fall(_d, _icon, _center));
        }

        private Vector2Int FreeCell(Vector2Int _c)
        {
            bool Occupied(Vector2Int _x) { return PickupItem.CellTaken(_x); }
            bool Ok(Vector2Int _x) { return _grid.IsSolid(_x) && !Occupied(_x); }
            if (Ok(_c)) return _c;
            for (int _r = 1; _r < Mathf.Max(_grid.Cols, _grid.Rows); _r++)
            {
                Vector2Int _best = _c; float _bestD = float.MaxValue;
                for (int _dy = -_r; _dy <= _r; _dy++)
                    for (int _dx = -_r; _dx <= _r; _dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(_dx), Mathf.Abs(_dy)) != _r) continue;
                        var _x = new Vector2Int(_c.x + _dx, _c.y + _dy);
                        float _dd = _dx * _dx + _dy * _dy;
                        if (_dd < _bestD && Ok(_x)) { _best = _x; _bestD = _dd; }
                    }
                if (_bestD < float.MaxValue) return _best;
            }
            return _grid.NearestSolid(_c);
        }

        private IEnumerator Fall(StaffDropEntry _d, Transform _icon, Vector3 _rest)
        {
            var _root = _d.Root.transform;
            float _y = _dropHeight, _v = _startSpeed;
            if (_icon) _icon.gameObject.SetActive(false);
            // 떨어지는 동안 빛 꼬리
            var _streak = ElementFx.Trail(Element.Electric, _root, 0.14f, 120f);
            var _sm = _streak.main; _sm.startColor = new ParticleSystem.MinMaxGradient(Color.white, _impactSparkColor);
            _streak.transform.localPosition = new Vector3(0, _staffLength * 0.5f, 0);
            while (_y > 0f && _root)
            {
                _v += _gravity * Time.deltaTime;
                _y = Mathf.Max(0f, _y - _v * Time.deltaTime);
                _root.position = _rest + Vector3.up * _y;
                yield return null;
            }
            if (!_root) yield break;
            var _em = _streak.emission; _em.rateOverTime = 0f; _streak.transform.SetParent(null, true); Destroy(_streak.gameObject, 0.8f);
            // 박힘: 먼지 고리 + 불티, 더 깊이 박혔다가 튕겨 올라옴
            ElementFx.Impact(_rest, _impactSparkColor, _impactDustColor, 1f);
            for (float _t = 0; _t < _impactReboundTime && _root; _t += Time.deltaTime)
            {
                float _u = _t / _impactReboundTime;
                float _sink = _impactSink * (1f - _u) * (1f - _u) * Mathf.Cos(_u * Mathf.PI * 1.5f);
                _root.position = _rest - Vector3.up * _sink;
                yield return null;
            }
            if (!_root) yield break;
            _root.position = _rest;
            _d.Landed = true; _d.LandTime = Time.time;
            var _pick = _root.GetComponent<PickupItem>(); if (_pick) _pick.Ready = true;
            Vector3 _axis = Quaternion.Euler(0, Random.Range(0f, 360f), 0) * Vector3.right;
            for (float _t = 0; _t < _wobbleTime && _root; _t += Time.deltaTime)
            {
                float _u = _t / _wobbleTime;
                _root.rotation = Quaternion.AngleAxis(_wobbleAngle * (1f - _u) * Mathf.Sin(_u * Mathf.PI * 5f), _axis);
                yield return null;
            }
            if (!_root) yield break;
            _root.rotation = Quaternion.identity;
            if (_icon) { _icon.gameObject.SetActive(true); StartCoroutine(Hover(_icon)); }
        }

        private IEnumerator Hover(Transform _icon)
        {
            Vector3 _basePos = _icon.localPosition;
            float _phase = Random.Range(0f, 6.28f);
            while (_icon)
            {
                float _t = Time.time * _iconBobSpeed + _phase;
                _icon.localPosition = _basePos + Vector3.up * Mathf.Sin(_t) * _iconBob;
                _icon.Rotate(Vector3.up, _iconSpin * Time.deltaTime, Space.World);
                yield return null;
            }
        }

        private void Update()
        {
            // 디스폰: 박힌 뒤 despawnTime초, 마지막 fadeOutTime초 동안 흐려짐
            for (int _i = _drops.Count - 1; _i >= 0; _i--)
            {
                StaffDropEntry _d = _drops[_i];
                if (!_d.Root) { _drops.RemoveAt(_i); continue; }
                if (!_d.Landed) continue;
                float _left = _despawnTime - (Time.time - _d.LandTime);
                if (_left <= 0f) { Remove(_i); continue; }
                if (_left < _fadeOutTime)
                {
                    float _k = _left / _fadeOutTime;
                    if (_d.Mat.HasProperty("_BaseColor")) { var _c = _d.Mat.GetColor("_BaseColor"); _c.a = _staffAlpha * _k; _d.Mat.SetColor("_BaseColor", _c); }
                    float _s = Mathf.Lerp(0.2f, 1f, _k);
                    var _icon = _d.Root.transform.Find("PatternIcon");
                    if (_icon) _icon.localScale = Vector3.one * _s;
                }
            }
        }

        private void Remove(int _i)
        {
            StaffDropEntry _d = _drops[_i];
            if (_d.Root) Destroy(_d.Root);
            if (_d.Mat) Destroy(_d.Mat);
            _drops.RemoveAt(_i);
        }

        private Transform MakeIcon(Transform _src, Transform _parent, int _pattern)
        {
            if (!_src) return null;
            var _mf = _src.GetComponent<MeshFilter>();
            var _mr = _src.GetComponent<MeshRenderer>();
            if (!_mf || !_mf.sharedMesh || !_mr) return null;
            var _go = new GameObject("PatternIcon");
            _go.transform.SetParent(_parent, false);
            var _vis = new GameObject("Mesh");
            _vis.transform.SetParent(_go.transform, false);
            _vis.AddComponent<MeshFilter>().sharedMesh = _mf.sharedMesh;
            var _r = _vis.AddComponent<MeshRenderer>();
            _r.sharedMaterials = _mr.sharedMaterials;
            _r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var _b = _mf.sharedMesh.bounds;
            float _s = _iconSize / Mathf.Max(_b.size.x, Mathf.Max(_b.size.y, _b.size.z), 1e-3f);
            var _rot = Quaternion.Euler(_iconEuler[Mathf.Clamp(_pattern, 0, 4)]);
            _vis.transform.localRotation = _rot;
            _vis.transform.localScale = Vector3.one * _s;
            _vis.transform.localPosition = -(_rot * _b.center) * _s;   // 회전한 메시 중심을 아이콘 중심에
            _go.transform.localPosition = new Vector3(0, _staffLength - _embedDepth + _iconGap + _iconSize * 0.5f, 0);
            return _go.transform;
        }

        private Material FadedMaterial()
        {
            if (_fadedMat) return _fadedMat;
            _fadedMat = new Material(_staffMaterial ? _staffMaterial : new Material(Shader.Find("Universal Render Pipeline/Lit")));
            _fadedMat.name = "Staff_Drop_Faded";
            _fadedMat.SetFloat("_Surface", 1f);
            _fadedMat.SetFloat("_Blend", 0f);
            _fadedMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _fadedMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _fadedMat.SetFloat("_ZWrite", 0f);
            _fadedMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _fadedMat.DisableKeyword("_ALPHATEST_ON");
            _fadedMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            _fadedMat.SetOverrideTag("RenderType", "Transparent");
            if (_fadedMat.HasProperty("_BaseColor")) { var _c = _fadedMat.GetColor("_BaseColor"); _c.a = _staffAlpha; _fadedMat.SetColor("_BaseColor", _c); }
            return _fadedMat;
        }
    }
}
