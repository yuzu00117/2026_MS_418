using System;
using System.Collections.Generic;
using Aura.ObjectInfo;
using Aura.Wave;
using UnityEngine;

namespace Aura.ObjectInfoDemo
{
    /// @brief ObjectInfo の情報だけで動くビーム。プレイヤーの手からも、フィールドのギミックからも出せる
    /// ・ビームの種類・強さ・届く距離は BeamSourceInfo（B1〜B3）から読む。切り替えは、BeamSourceInfo を付けた物を選び直す
    /// ・跳ね返るかは、当たった物の素材（Substance の SubstanceType、O6）で決める。素材の無い物では止まる
    /// ・当たった物に WaveReaction（O3、O4）と ReactionTrigger があれば、ビームを渡す。弱点（WeakPoint、O5）なら強さに倍率を掛ける
    /// 今ある WaveEmitter（Aura.Wave）とは別物で、WaveSurface や WaveReceiver は見ない
    public sealed class InfoBeamEmitter : MonoBehaviour
    {
        private const int MaxReflectionLimit = 16;           ///< Max Reflections に決められる最大の回数
        private const float SurfaceOffset = 0.01f;           ///< 跳ね返った後、次の判定を始める位置を面から離す距離（m）
        private const float MinNormalSqrMagnitude = 0.5f;    ///< 面の法線の長さの 2 乗がこれより小さければ、向きが取れなかったとみなす
        private const float MinTargetSqrDistance = 0.0001f;  ///< 手の先から狙う先までの距離の 2 乗（m^2）がこれより小さければ、向きが決まらないとみなす

        [Header("ビームの種類")]
        [Tooltip("出せるビーム。BeamSourceInfo を付けた GameObject（子に置く）を並べる。種類・強さ・届く距離は、ここから読む\n" +
                 "切り替えると上から順に入れ替わり、最後の次は最初に戻る。空だと出せない")]
        [SerializeField] private BeamSourceInfo[] _sources;           ///< 出せるビーム（BeamSourceInfo）

        [Tooltip("今のビームの番号（Sources の何番目か。0 から数える）。実行中に変えると、そのビームに切り替わる")]
        [SerializeField, Min(0)] private int _sourceIndex;            ///< 今のビームの番号

        [Tooltip("出したもの。出し始めるときに、BeamSourceInfo の Source Kind に入れる（シートの B5）\n" +
                 "受け手の反応には使わない（受け手は波の種類だけで反応する）")]
        [SerializeField] private BeamSourceKind _sourceKind = BeamSourceKind.Player;  ///< 出したもの

        [Tooltip("オンにすると、始めからビームを出し続ける（フィールドのギミック用）。入力の部品が無くても出る\n" +
                 "Set Trigger Held を UnityEvent から呼ぶと、出す・止めるを切り替えられる")]
        [SerializeField] private bool _isFiringOnStart;               ///< 始めからビームを出し続けるか

        [Header("狙い")]
        [Tooltip("ビームが出る位置の候補（手の先に置いた空の GameObject など）。上から見て、アクティブな最初のものを使う\n" +
                 "一人称と三人称で手が変わるときは両方入れておく（ViewModeSwitcher が出し入れした方が使われる）\n" +
                 "Aim Origins が空のときは、この正面（青い矢印の向き）に出す。空なら、この GameObject の位置から出る")]
        [SerializeField] private Transform[] _muzzles;                ///< ビームが出る位置の候補

        [Tooltip("狙いを決める視点の候補（ふつうはカメラ）。アクティブな最初のものの正面で最初に当たる位置へ向けて出すので、画面の中心（照準）に飛ぶ\n" +
                 "空なら、Muzzle の正面に出す（ギミックのビームはこちら）")]
        [SerializeField] private Transform[] _aimOrigins;             ///< 狙いを決める視点の候補

        [Tooltip("この GameObject とその子のコライダーには、ビームが当たらない（自分の体に当たらないようにする）\n" +
                 "跳ね返って戻ってきたビームは、ここの下のコライダーに当たると止まる\n" +
                 "空なら、この GameObject を使う")]
        [SerializeField] private Transform _ignoreRoot;               ///< この下のコライダーに当たらない。空なら自分

        [Tooltip("ビームが当たるレイヤー。チェックを外したレイヤーの物は、ビームが素通りする")]
        [SerializeField] private LayerMask _hitMask = ~0;             ///< ビームが当たるレイヤー

        [Tooltip("Is Trigger をオンにしたコライダーに、ビームが当たるか")]
        [SerializeField] private QueryTriggerInteraction _triggerInteraction = QueryTriggerInteraction.Ignore;  ///< トリガーに当たるか

        [Header("ビーム")]
        [Tooltip("当たり判定の太さ（球の半径、m）。大きいほど、狙いが少しずれても当たる。0 なら太さのない線で判定する")]
        [SerializeField, Min(0.0f)] private float _radius = 0.1f;     ///< 当たり判定の半径（m）

        [Tooltip("1 本のビームが跳ね返る回数の上限（回。1〜16）。この回数だけ跳ね返った後は、次に当たった物で止まる")]
        [SerializeField, Range(1, MaxReflectionLimit)] private int _maxReflections = 8;  ///< 跳ね返る回数の上限

        [Tooltip("ビームの線のマテリアル。空なら既定のマテリアルを使い、WaveType の Color で色を付ける")]
        [SerializeField] private Material _lineMaterial;              ///< ビームの線のマテリアル。空なら既定のもの

        private readonly List<Vector3> _corners = new List<Vector3>();                         ///< ビームの線を描く折れ線の点。毎フレーム使い回す
        private readonly List<InfoBeamHit> _lastHits = new List<InfoBeamHit>();                ///< 最後のフレームでビームが当たった物（当たった順）
        private readonly HashSet<ReactionTrigger> _deliveredTriggers = new HashSet<ReactionTrigger>();  ///< このフレームで、もうビームを渡した受け手
        private WaveLine _beamLine;
        private int _appliedSourceIndex;  ///< 最後に反映したビームの番号。Inspector での変更に気付くために持つ
        private bool _isTriggerHeld;      ///< 引き金を引いているか

        /// @brief ビームを選び直した後に起きる
        public event Action<BeamSourceInfo> SourceChanged;

        /// @brief 今のビーム（BeamSourceInfo）。1 つもなければ null
        public BeamSourceInfo CurrentSource => HasSources ? _sources[_sourceIndex] : null;

        /// @brief 今のビームの番号
        public int SourceIndex => _sourceIndex;

        /// @brief 出せるビームの数
        public int SourceCount => _sources != null ? _sources.Length : 0;

        /// @brief ビームを出しているか
        public bool IsFiring => _isTriggerHeld && CurrentType != null && isActiveAndEnabled;

        /// @brief 最後のフレームでビームが当たった物（当たった順）。出していないときは空
        public IReadOnlyList<InfoBeamHit> LastHits => _lastHits;

        /// @brief 今ビームが出る位置
        public Transform CurrentMuzzle => FindFirstActive(_muzzles) ?? transform;

        /// @brief 今の狙いを決める視点。無ければ null
        public Transform CurrentAimOrigin => FindFirstActive(_aimOrigins);

        /// @brief この下のコライダーには、ビームが当たらない
        public Transform IgnoreRoot => _ignoreRoot != null ? _ignoreRoot : transform;

        private bool HasSources => _sources != null && _sources.Length > 0;

        private WaveType CurrentType
        {
            get
            {
                BeamSourceInfo source = CurrentSource;
                return source != null ? source.WaveType : null;
            }
        }

        //============================================================
        // 公開の操作
        //============================================================

        /// @brief 引き金の状態を伝える。入力の部品から毎フレーム呼んでよい。押している間ビームを出す
        /// ギミックのビームなら、UnityEvent（スイッチの On Activated など）から呼んで、出す・止めるを切り替える
        /// @param isHeld 引き金を引いているか
        public void SetTriggerHeld(bool isHeld)
        {
            if (_isTriggerHeld == isHeld)
            {
                return;
            }
            _isTriggerHeld = isHeld;
            if (isHeld)
            {
                MarkSourceKind();
            }
            else
            {
                HideBeam();
            }
        }

        /// @brief 次のビームに切り替える。最後の次は最初に戻る
        public void SelectNextSource()
        {
            if (HasSources)
            {
                SelectSource((_sourceIndex + 1) % _sources.Length);
            }
        }

        /// @brief 前のビームに切り替える。最初の前は最後に戻る
        public void SelectPreviousSource()
        {
            if (HasSources)
            {
                SelectSource((_sourceIndex - 1 + _sources.Length) % _sources.Length);
            }
        }

        /// @brief 番号を指定してビームを切り替える
        /// @param index ビームの番号
        /// @return 切り替えたら true。範囲の外なら false
        public bool SelectSource(int index)
        {
            if (!HasSources || index < 0 || index >= _sources.Length)
            {
                return false;
            }
            _sourceIndex = index;
            ApplySourceIndex();
            return true;
        }

        //============================================================
        // Unity のメッセージ
        //============================================================

        private void Awake()
        {
            _beamLine = WaveLine.Create("InfoBeam", transform, _lineMaterial);
            _beamLine.Hide();
            _sourceIndex = HasSources ? Mathf.Clamp(_sourceIndex, 0, _sources.Length - 1) : 0;
            _appliedSourceIndex = _sourceIndex;
        }

        private void Start()
        {
            if (_isFiringOnStart)
            {
                SetTriggerHeld(true);
            }
        }

        private void OnDisable()
        {
            HideBeam();
        }

        private void Update()
        {
            // Inspector で実行中に変えた番号を反映する
            if (_sourceIndex != _appliedSourceIndex)
            {
                _sourceIndex = HasSources ? Mathf.Clamp(_sourceIndex, 0, _sources.Length - 1) : 0;
                ApplySourceIndex();
            }
        }

        private void LateUpdate()
        {
            // カメラと手が動いた後に、ビームの判定と見た目を合わせる
            if (IsFiring)
            {
                UpdateBeam(Time.deltaTime);
            }
            else
            {
                HideBeam();
            }
        }

        //============================================================
        // 内部の処理
        //============================================================

        private void UpdateBeam(float deltaTime)
        {
            BeamSourceInfo source = CurrentSource;
            Transform muzzle = CurrentMuzzle;
            float range = source.Range;
            TraceBeam(source, muzzle.position, GetAimDirection(muzzle, range), range, source.EnergyPerSecond * deltaTime);

            // 受け手の処理でビームが止められた（この部品が無効にされたなど）ときは、線を出し直さない
            if (!IsFiring)
            {
                return;
            }
            WaveType type = source.WaveType;
            _beamLine.Draw(_corners, muzzle.up, type, Time.time * type.ScrollSpeed, isPacket: false);
        }

        /// @brief ビームの通り道をたどり、当たった物にビームを渡す。跳ね返るかは素材で決める
        private void TraceBeam(BeamSourceInfo source, Vector3 origin, Vector3 direction, float range, float energy)
        {
            _corners.Clear();
            _lastHits.Clear();
            _deliveredTriggers.Clear();
            _corners.Add(origin);

            Vector3 position = origin;
            float remaining = range;
            int reflectionCount = 0;
            // 1 回まわるごとに、止まるか、跳ね返る（Max Reflections 回まで）か、残りを進みきって抜けるので、必ず終わる
            while (true)
            {
                // 跳ね返った後は、出した本人（Ignore Root の下）にも当たるようにして、当たったらそこで止める
                Transform ignoreRoot = reflectionCount == 0 ? IgnoreRoot : null;
                if (!BeamPhysics.TryCast(position, direction, _radius, remaining, _hitMask, _triggerInteraction,
                        ignoreRoot, out RaycastHit hit))
                {
                    _corners.Add(position + direction * remaining);
                    return;
                }

                remaining -= hit.distance;
                _corners.Add(hit.point);
                if (reflectionCount > 0 && hit.collider.transform.IsChildOf(IgnoreRoot))
                {
                    return;
                }

                bool isReflected = CanReflect(hit, direction, reflectionCount, out Vector3 normal);
                Deliver(source.WaveType, energy, hit, reflectionCount, isReflected);
                if (!isReflected || remaining <= 0.0f)
                {
                    return;
                }

                // 当たったときの当たり判定の球の中心から、面の外へ少し離して続ける
                position += direction * hit.distance + normal * SurfaceOffset;
                direction = Vector3.Reflect(direction, normal).normalized;
                ++reflectionCount;
            }
        }

        /// @brief 当たった物で跳ね返るかを決める。素材（SubstanceType）が跳ね返すものなら跳ね返り、素材が無い物では止まる
        /// @param outNormal 跳ね返る向きを決める面の法線。進む向きの手前側を向くようにそろえる
        private bool CanReflect(in RaycastHit hit, Vector3 direction, int reflectionCount, out Vector3 outNormal)
        {
            outNormal = hit.normal;
            if (reflectionCount >= _maxReflections)
            {
                return false;
            }
            // 始めから重なっていた物（距離 0）と、面の向きが取れない物では跳ね返らない
            // NaN の法線でも止まるよう、比べた結果を否定して判定する
            if (hit.distance <= 0.0f || !(outNormal.sqrMagnitude >= MinNormalSqrMagnitude))
            {
                return false;
            }
            // 面の裏から当たったときも、来た側へ跳ね返す
            if (Vector3.Dot(outNormal, direction) > 0.0f)
            {
                outNormal = -outNormal;
            }
            return ObjectInfoLookup.IsWaveReflective(hit.collider, false);
        }

        /// @brief 当たった物にビームを渡す。弱点なら倍率を掛け、WaveReaction の受け手があれば渡す
        private void Deliver(WaveType type, float energy, in RaycastHit hit, int reflectionCount, bool isReflected)
        {
            float multiplier = ObjectInfoLookup.TryGet(hit.collider, out WeakPoint weakPoint)
                ? weakPoint.GetMultiplier(type)
                : WeaknessType.NormalMultiplier;
            var beamHit = new InfoBeamHit(type, energy * multiplier, multiplier, hit.point, hit.collider,
                reflectionCount, isReflected);
            _lastHits.Add(beamHit);

            // 同じ受け手には、1 本のビーム（1 フレーム）で 1 回だけ渡す（跳ね返って、また同じ受け手に当たっても渡さない）
            if (ObjectInfoLookup.TryGet(hit.collider, out WaveReaction reaction)
                && ReactionTrigger.TryGetTrigger(reaction, out ReactionTrigger trigger)
                && _deliveredTriggers.Add(trigger))
            {
                trigger.ReceiveBeam(beamHit);
            }
        }

        /// @brief ビームを出す向きを決める。視点があれば、視点の正面で当たる位置へ Muzzle から向ける
        private Vector3 GetAimDirection(Transform muzzle, float range)
        {
            Transform aimOrigin = CurrentAimOrigin;
            if (aimOrigin == null)
            {
                return muzzle.forward;
            }

            Vector3 aimPosition = aimOrigin.position;
            Vector3 aimForward = aimOrigin.forward;
            float aimRange = range + Vector3.Distance(aimPosition, muzzle.position);
            Vector3 target = BeamPhysics.TryCast(aimPosition, aimForward, 0.0f, aimRange, _hitMask,
                _triggerInteraction, IgnoreRoot, out RaycastHit hit)
                ? hit.point
                : aimPosition + aimForward * aimRange;

            Vector3 toTarget = target - muzzle.position;
            // 狙う先が手より後ろ（壁に張り付いているときなど）なら、視点の向きにそのまま出す
            if (toTarget.sqrMagnitude < MinTargetSqrDistance || Vector3.Dot(toTarget, aimForward) <= 0.0f)
            {
                return aimForward;
            }
            return toTarget.normalized;
        }

        private void ApplySourceIndex()
        {
            if (_sourceIndex == _appliedSourceIndex)
            {
                return;
            }
            _appliedSourceIndex = _sourceIndex;
            if (_isTriggerHeld)
            {
                MarkSourceKind();
            }
            SourceChanged?.Invoke(CurrentSource);
        }

        /// @brief 今のビームの BeamSourceInfo に、出したもの（B5）を入れる
        private void MarkSourceKind()
        {
            BeamSourceInfo source = CurrentSource;
            if (source != null)
            {
                source.SourceKind = _sourceKind;
            }
        }

        private void HideBeam()
        {
            _lastHits.Clear();
            if (_beamLine != null)
            {
                _beamLine.Hide();
            }
        }

        private static Transform FindFirstActive(Transform[] candidates)
        {
            if (candidates == null)
            {
                return null;
            }
            foreach (Transform candidate in candidates)
            {
                if (candidate != null && candidate.gameObject.activeInHierarchy)
                {
                    return candidate;
                }
            }
            return null;
        }
    }
}
