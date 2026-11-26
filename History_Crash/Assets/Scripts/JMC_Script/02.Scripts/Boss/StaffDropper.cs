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
    /// 보스 패턴이 끝난 자리에 그 패턴의 무기 오브젝트를 떨어뜨리고, Player가 E키로 줍게 한다.
    /// - 하늘에서 빠르게 떨어져 타일 정중앙에 닿은 뒤(다른 아이템이 있는 칸이면 가장 가까운 빈 칸) 살짝 떠올라 돈다.
    /// - 떨어진 뒤 despawnTime초가 지나면 작아지며 사라진다.
    /// - Player 칸에서 pickupRange칸 안의 무기는 E키로 획득(가장 가까운 것).
    /// </summary>
    public class StaffDropper : MonoBehaviour
    {
        [SerializeField, FormerlySerializedAs("grid")] private TileGrid _grid;

        [Header("착지 연출")]
        [SerializeField, FormerlySerializedAs("impactSink"), Tooltip("닿는 순간 눌렸다가 튕겨 올라오는 깊이(m)")] private float _impactSink = 0.12f;
        [SerializeField, FormerlySerializedAs("impactReboundTime")] private float _impactReboundTime = 0.22f;
        [SerializeField, FormerlySerializedAs("impactSparkColor")] private Color _impactSparkColor = new Color(1f, 0.78f, 0.35f);
        [SerializeField, FormerlySerializedAs("impactDustColor")] private Color _impactDustColor = new Color(0.32f, 0.28f, 0.22f, 0.55f);

        [Header("낙하")]
        [SerializeField, FormerlySerializedAs("dropHeight")] private float _dropHeight = 12f;
        [SerializeField, FormerlySerializedAs("startSpeed")] private float _startSpeed = 18f;
        [SerializeField, FormerlySerializedAs("gravity")] private float _gravity = 90f;
        [SerializeField, FormerlySerializedAs("wobbleTime"), Tooltip("박힐 때 살짝 흔들리는 시간(초)")] private float _wobbleTime = 0.35f;
        [SerializeField, FormerlySerializedAs("wobbleAngle")] private float _wobbleAngle = 10f;

        [Header("무기 오브젝트")]
        [SerializeField, Tooltip("A~E 크기(가장 긴 변, m). 가는 A·E는 길게")] private float[] _weaponSizes = { 1.4f, 0.85f, 0.85f, 0.75f, 1.8f };
        [SerializeField, Tooltip("착지 뒤 바닥에서 띄우는 높이(m)")] private float _hoverGap = 0.2f;
        [SerializeField, FormerlySerializedAs("iconBob")] private float _iconBob = 0.06f;
        [SerializeField, FormerlySerializedAs("iconBobSpeed")] private float _iconBobSpeed = 2.2f;
        [SerializeField, FormerlySerializedAs("iconSpin")] private float _iconSpin = 70f;

        [Header("위치 표시: 스포트라이트 + 블룸(떨어진 무기를 짚어 줌)")]
        [SerializeField, Tooltip("스포트라이트 색")] private Color _spotColor = new Color(1f, 0.9f, 0.7f, 1f);
        [SerializeField, Tooltip("바닥에서 조명까지 높이(m)")] private float _spotHeight = 2.8f;
        [SerializeField, Tooltip("조명 세기")] private float _spotIntensity = 2.5f;
        [SerializeField, Tooltip("비추는 원뿔 각도(°). 바닥 원 지름 ≈ 2 × 높이 × tan(각도/2)")] private float _spotAngle = 16f;
        [SerializeField, Tooltip("가장자리 부드러움(내부 각도 비율 0~1). 낮을수록 가운데만 쨍하고 바깥으로 갈수록 어두워짐")] private float _spotInnerRatio = 0.8f;
        [SerializeField, Tooltip("무기 앞 점광원 세기(무기가 가장 밝게)")] private float _coreLight = 1.2f;
        [SerializeField, Tooltip("둥근 빛무리 지름(무기 크기의 배수)")] private float _glowScale = 1.5f;
        [SerializeField, Range(0f, 1f), Tooltip("둥근 빛무리 진하기")] private float _glowAlpha = 0.2f;
        [SerializeField, Tooltip("옆으로 퍼지는 빛 줄기 폭·두께(m)")] private Vector2 _spotStreakSize = new Vector2(1.4f, 0.08f);
        [SerializeField, Range(0f, 1f), Tooltip("옆으로 퍼지는 빛 줄기 진하기")] private float _spotStreakAlpha = 0f;
        [SerializeField, FormerlySerializedAs("_pillarAppearTime"), Tooltip("박힌 뒤 빛이 켜지는 시간(초)")] private float _spotAppearTime = 0.4f;
        [SerializeField, FormerlySerializedAs("_pillarPulseSpeed"), Tooltip("맥박 속도(회/초)")] private float _spotPulseSpeed = 0.8f;

        [Header("유지·획득")]
        [SerializeField, FormerlySerializedAs("despawnTime"), Tooltip("박힌 뒤 사라지기까지(초)")] private float _despawnTime = 10f;
        [SerializeField, FormerlySerializedAs("fadeOutTime"), Tooltip("사라질 때 흐려지는 시간(초)")] private float _fadeOutTime = 1f;
        // 획득은 PlayerLoadout(E키)가 PickupItem을 통해 처리한다

        private static readonly string[] _names = { "A", "B", "C", "D", "E" };
        // 패턴 오브젝트를 세워서 보이게 하는 회전(메시 기준). B·D는 장면 배치와 같은 회전
        private static readonly Vector3[] _iconEuler = { new Vector3(0, 0, 90), new Vector3(90, 0, 0), new Vector3(90, 0, 0), new Vector3(90, 0, 0), new Vector3(180, 0, 0) };
        private readonly List<StaffDropEntry> _drops = new List<StaffDropEntry>();

        private const string SPOT_NAME = "Spotlight";
        private static Material _glowMat;
        private static readonly int _baseColorId = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock _glowBlock;

        public int Count { get { return _drops.Count; } }

        /// <summary>pattern(0=A~4=E)의 무기를 world 위치 칸 중앙(차 있으면 가장 가까운 빈 칸)에 떨어뜨린다.</summary>
        public void Drop(int _pattern, Vector3 _world, Transform _iconSource)
        {
            if (_grid == null) return;
            // 패턴에 맞아 죽는 순간 게임이 끝나 이 오브젝트가 꺼지면(ActiveOnlyWhilePlaying) 코루틴을 시작할 수 없어 에러가 남 → 떨어뜨리지 않음
            if (!isActiveAndEnabled) return;
            var _cell = FreeCell(_grid.WorldToCell(_world));
            Vector3 _center = _grid.CellCenter(_cell, _grid.FloorTopY);

            var _root = new GameObject("Staff_Drop_" + _names[Mathf.Clamp(_pattern, 0, 4)]);
            _root.transform.position = _center;
            Transform _icon = MakeIcon(_iconSource, _root.transform, _pattern);
            MakeSpot(_root.transform, _weaponSizes[Mathf.Clamp(_pattern, 0, 4)], (_icon ? _icon.localPosition.y : 0.4f) + _hoverGap).gameObject.SetActive(false);   // 위치 표시: 박힌 뒤 켠다
            var _item = _root.AddComponent<PickupItem>();
            _item.Kind = PickupKind.Staff; _item.Value = _pattern; _item.Cell = _cell; _item.Ready = false;
            StaffDropEntry _d = new StaffDropEntry { Root = _root, Pattern = _pattern, Cell = _cell };
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
            HistoryCrash.Sound.GameSfx.Play("Weapon_Fall", _root);
            // 떨어지는 동안 빛 꼬리
            var _streak = ElementFx.Trail(Element.Electric, _root, 0.14f, 120f);
            var _sm = _streak.main; _sm.startColor = new ParticleSystem.MinMaxGradient(Color.white, _impactSparkColor);
            _streak.transform.localPosition = _icon ? _icon.localPosition : Vector3.up * 0.5f;
            while (_y > 0f && _root)
            {
                _v += _gravity * Time.deltaTime;
                _y = Mathf.Max(0f, _y - _v * Time.deltaTime);
                _root.position = _rest + Vector3.up * _y;
                yield return null;
            }
            if (!_root) yield break;
            var _em = _streak.emission; _em.rateOverTime = 0f; _streak.transform.SetParent(null, true); Destroy(_streak.gameObject, 0.8f);
            // 착지: 먼지 고리 + 불티, 눌렸다가 튕겨 올라옴
            ElementFx.Impact(_rest, _impactSparkColor, _impactDustColor, 1f);
            HistoryCrash.Sound.GameSfx.Play("Weapon_Land", _rest);
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
            var _spot = _root.Find(SPOT_NAME); if (_spot) _spot.gameObject.SetActive(true);
            Vector3 _axis = Quaternion.Euler(0, Random.Range(0f, 360f), 0) * Vector3.right;
            for (float _t = 0; _t < _wobbleTime && _root; _t += Time.deltaTime)
            {
                float _u = _t / _wobbleTime;
                _root.rotation = Quaternion.AngleAxis(_wobbleAngle * (1f - _u) * Mathf.Sin(_u * Mathf.PI * 5f), _axis);
                yield return null;
            }
            if (!_root) yield break;
            _root.rotation = Quaternion.identity;
            if (_icon) StartCoroutine(Hover(_icon));
        }

        private IEnumerator Hover(Transform _icon)
        {
            Vector3 _basePos = _icon.localPosition;
            for (float _t = 0f; _icon; _t += Time.deltaTime)
            {
                _icon.localPosition = _basePos + Vector3.up * (_hoverGap * Mathf.SmoothStep(0f, 1f, _t / 0.5f) + Mathf.Sin(_t * _iconBobSpeed) * _iconBob);
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
                UpdateSpot(_d, _left);
                if (_left < _fadeOutTime)
                {
                    float _k = _left / _fadeOutTime;
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
            _drops.RemoveAt(_i);
        }

        // ---------- 위치 표시 스포트라이트: 좁은 원뿔로 무기를 짚고, 무기 중심이 가장 밝으며 옆으로 블룸처럼 번진다 ----------
        // _size: 무기 크기, _height: 떠 있는 무기 중심 높이
        private Transform MakeSpot(Transform _parent, float _size, float _height)
        {
            var _root = new GameObject(SPOT_NAME).transform;
            _root.SetParent(_parent, false);

            var _beam = new GameObject("Beam");
            _beam.transform.SetParent(_root, false);
            _beam.transform.localPosition = Vector3.up * _spotHeight;
            _beam.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // 아래를 향함
            var _l = _beam.AddComponent<Light>();
            _l.type = LightType.Spot;
            _l.color = _spotColor;
            _l.intensity = 0f;   // 박힌 뒤 UpdateSpot에서 서서히 켬
            _l.range = _spotHeight + 1.5f;
            _l.spotAngle = _spotAngle;
            _l.innerSpotAngle = _spotAngle * Mathf.Clamp01(_spotInnerRatio);
            _l.shadows = LightShadows.None;

            var _core = new GameObject("Core");
            _core.transform.SetParent(_root, false);
            _core.transform.localPosition = Vector3.up * _height;
            var _cl = _core.AddComponent<Light>();
            _cl.type = LightType.Point;
            _cl.color = _spotColor;
            _cl.intensity = 0f;
            _cl.range = _size + 0.6f;
            _cl.shadows = LightShadows.None;

            MakeGlowQuad(_root, "Glow", Vector2.one * _size * _glowScale, _height);
            MakeGlowQuad(_root, "Streak", _spotStreakSize, _height);
            return _root;
        }

        private void MakeGlowQuad(Transform _parent, string _name, Vector2 _size, float _height)
        {
            var _go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _go.name = _name;
            Destroy(_go.GetComponent<Collider>());
            _go.transform.SetParent(_parent, false);
            _go.transform.localPosition = Vector3.up * _height;
            _go.transform.localScale = new Vector3(_size.x, _size.y, 1f);
            var _r = _go.GetComponent<MeshRenderer>();
            _r.sharedMaterial = GlowMaterial();
            _r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _r.receiveShadows = false;
            _r.enabled = false;
        }

        private void UpdateSpot(StaffDropEntry _d, float _left)
        {
            var _spot = _d.Root.transform.Find(SPOT_NAME);
            if (!_spot || !_spot.gameObject.activeSelf) return;
            float _appear = Mathf.Clamp01((Time.time - _d.LandTime) / Mathf.Max(_spotAppearTime, 0.01f));
            float _fade = _left < _fadeOutTime ? _left / _fadeOutTime : 1f;
            float _pulse = 0.9f + 0.1f * Mathf.Sin((Time.time - _d.LandTime) * _spotPulseSpeed * Mathf.PI * 2f);
            float _k = _appear * _fade * _pulse;

            var _beam = _spot.Find("Beam");
            if (_beam)
            {
                _beam.GetComponent<Light>().intensity = _spotIntensity * _k;
                _beam.rotation = Quaternion.Euler(90f, 0f, 0f);   // 박힐 때 흔들려도 항상 수직으로
                _beam.position = _spot.position + Vector3.up * _spotHeight;
            }
            var _core = _spot.Find("Core");
            var _cam = Camera.main;
            if (_core)
            {
                _core.GetComponent<Light>().intensity = _coreLight * _k;
                // 무기 안이 아니라 화면 쪽 앞에서 비춰야 보이는 면이 밝다
                if (_cam) _core.position = _spot.Find("Glow").position + (_cam.transform.position - _spot.position).normalized * 0.5f;
            }

            if (_glowBlock == null) _glowBlock = new MaterialPropertyBlock();
            for (int _i = 0; _i < _spot.childCount; _i++)
            {
                var _q = _spot.GetChild(_i);
                if (_q.name != "Glow" && _q.name != "Streak") continue;
                if (_cam) _q.rotation = Quaternion.LookRotation(_q.position - _cam.transform.position, _cam.transform.up);   // 항상 화면을 보고, 줄기는 화면 가로로
                var _r = _q.GetComponent<Renderer>();
                var _c = _spotColor; _c.a = (_q.name == "Glow" ? _glowAlpha : _spotStreakAlpha) * _k;
                _r.enabled = _c.a > 0.003f;
                _glowBlock.SetColor(_baseColorId, _c);
                _r.SetPropertyBlock(_glowBlock);
            }
        }

        private static Material GlowMaterial()
        {
            if (_glowMat) return _glowMat;
            // 가운데가 가장 진하고 바깥으로 빠르게 옅어지는 둥근 그라데이션(블룸 느낌)
            const int SIZE = 64;
            var _tex = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Staff_Spot_Glow" };
            for (int _y = 0; _y < SIZE; _y++)
                for (int _x = 0; _x < SIZE; _x++)
                {
                    float _dx = (_x + 0.5f) / SIZE * 2f - 1f, _dy = (_y + 0.5f) / SIZE * 2f - 1f;
                    float _t = Mathf.Clamp01(1f - Mathf.Sqrt(_dx * _dx + _dy * _dy));
                    _tex.SetPixel(_x, _y, new Color(1f, 1f, 1f, _t * _t * _t));
                }
            _tex.Apply();
            _glowMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "Staff_Drop_SpotGlow" };
            _glowMat.SetTexture("_BaseMap", _tex);
            _glowMat.SetColor(_baseColorId, Color.white);
            _glowMat.SetFloat("_Surface", 1f);
            _glowMat.SetFloat("_Blend", 2f);
            _glowMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _glowMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);   // 가산
            _glowMat.SetFloat("_ZWrite", 0f);
            _glowMat.SetFloat("_Cull", 0f);   // 양면
            _glowMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _glowMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return _glowMat;
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
            float _s = _weaponSizes[Mathf.Clamp(_pattern, 0, 4)] / Mathf.Max(_b.size.x, Mathf.Max(_b.size.y, _b.size.z), 1e-3f);
            var _rot = Quaternion.Euler(_iconEuler[Mathf.Clamp(_pattern, 0, 4)]);
            _vis.transform.localRotation = _rot;
            _vis.transform.localScale = Vector3.one * _s;
            _vis.transform.localPosition = -(_rot * _b.center) * _s;   // 회전한 메시 중심을 아이콘 중심에
            _go.transform.localPosition = new Vector3(0, Mathf.Abs((_rot * _b.size).y) * _s * 0.5f, 0);   // 아래 끝이 바닥에
            return _go.transform;
        }
    }
}
