using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace BricksBreakerDemo
{
    [RequireComponent(typeof(Collider2D))]
    public class Brick : MonoBehaviour
    {
        #region 配置 - 下落入场

        [SerializeField] private Transform _visual;
        [SerializeField] private float fallHeight = 8f;
        [SerializeField] private float fallDuration = 0.7f;
        [SerializeField] private float maxStagger = 0.4f;
        [SerializeField] private float rotationRange = 45f;
        [SerializeField] private float startScale = 0.2f;

        #endregion

        #region 配置 - 碎片

        [SerializeField] private Material fragmentMaterial;       // FragmentUnlit2D 材质（Inspector 拖入）
        [SerializeField] private Color fragmentTint = new Color(0.7f, 0.7f, 0.8f, 1f); // juicy DARKEN

        #endregion

        #region 配置 - 碎片物理（世界单位/秒）

        [SerializeField] private float pushSpeed = 5f;
        [SerializeField] private float fragGravity = 24f;
        [SerializeField] private float fragDamping = 0.6f;
        [SerializeField] private float fragAngular = 300f;
        [SerializeField] private float fragLifetime = 2f;

        #endregion

        #region 配置 - 粒子

        [SerializeField] private int shatterParticleCount = 5;
        [SerializeField] private float shatterParticleSpeed = 3f;
        [SerializeField] private float particleSize = 0.5f;

        #endregion

        #region 状态

        private CancellationTokenSource _cts;
        private static Transform _container;
        private static Sprite _squareSprite;
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        #endregion

        #region 生命周期

        private void Start() => PlaySpawnAnimation();

        private void OnCollisionEnter2D(Collision2D other)
        {
            if (other.rigidbody != null && other.rigidbody.TryGetComponent<Ball>(out _))
                Shatter(other.transform.position, other.rigidbody.linearVelocity);
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

        #endregion

        #region 下落入场动画

        public void PlaySpawnAnimation()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            PlayFallAsync(_cts.Token).Forget();
        }

        private async UniTaskVoid PlayFallAsync(CancellationToken ct)
        {
            if (_visual == null) return;

            Vector3 restPos = Vector3.zero;
            Vector3 startPos = restPos + Vector3.up * fallHeight;
            float startRotZ = UnityEngine.Random.Range(-rotationRange, rotationRange);
            Vector3 startScaleVec = Vector3.one * startScale;

            _visual.localPosition = startPos;
            _visual.localRotation = Quaternion.Euler(0f, 0f, startRotZ);
            _visual.localScale = startScaleVec;

            await UniTask.Delay(TimeSpan.FromSeconds(UnityEngine.Random.value * maxStagger), cancellationToken: ct);

            float elapsed = 0f;
            while (elapsed < fallDuration)
            {
                elapsed += Time.deltaTime;
                float k = EaseOutBack(Mathf.Clamp01(elapsed / fallDuration));
                _visual.localPosition = Vector3.LerpUnclamped(startPos, restPos, k);
                _visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(startRotZ, 0f, k));
                _visual.localScale = Vector3.LerpUnclamped(startScaleVec, Vector3.one, k);
                await UniTask.Yield(ct);
            }

            _visual.localPosition = restPos;
            _visual.localRotation = Quaternion.identity;
            _visual.localScale = Vector3.one;
        }

        #endregion

        #region 碎裂（真·多边形切片，移植自 juicy LineSliceObject）

        private void Shatter(Vector2 ballWorldPos, Vector2 ballWorldVel)
        {
            var vsr = _visual != null ? _visual.GetComponent<SpriteRenderer>() : null;
            Color brickColor = vsr != null ? vsr.color : Color.white;

            if (vsr != null && vsr.sprite != null && _visual != null && fragmentMaterial != null)
            {
                Sprite sprite = vsr.sprite;
                Color fragColor = brickColor * fragmentTint;

                // 用外接矩形的 4 角（匹配 juicy SliceEffect 构造函数）
                GetRectFromSprite(sprite, out var rectVerts, out var rectUvs);

                // 切线 = 球的轨迹（穿过砖块）
                Vector2 lp = _visual.InverseTransformPoint(ballWorldPos);
                Vector2 ld = _visual.InverseTransformDirection(ballWorldVel);
                if (ld.sqrMagnitude < 0.0001f) ld = Vector2.right;
                ld.Normalize();
                Vector2 p1 = lp + ld * 50f, p2 = lp - ld * 50f;

                // PUSH：球→砖方向
                Vector2 pushDir = ((Vector2)_visual.position - ballWorldPos).normalized;
                Vector2 pushVel = pushDir * pushSpeed;

                // SHATTER 分离力：切线垂直方向
                Vector2 perpWorld = _visual.TransformDirection(new Vector2(-ld.y, ld.x)).normalized;
                Vector2 shatterVel = perpWorld * pushSpeed * 0.4f;

                if (Slice(rectVerts, rectUvs, p1, p2, out var v1, out var u1, out var v2, out var u2))
                {
                    // 切成功：2 块多边形碎片
                    float a1 = UnityEngine.Random.value > 0.5f ? fragAngular : -fragAngular;
                    float a2 = UnityEngine.Random.value > 0.5f ? fragAngular : -fragAngular;
                    SpawnSlice(v1, u1, sprite, fragColor, pushVel + shatterVel, a1);
                    SpawnSlice(v2, u2, sprite, fragColor, pushVel - shatterVel, a2);
                }
                else
                {
                    // 切失败兜底：整块矩形
                    float a = UnityEngine.Random.value > 0.5f ? fragAngular : -fragAngular;
                    SpawnSlice(new List<Vector2>(rectVerts), new List<Vector2>(rectUvs),
                        sprite, fragColor, pushVel, a);
                }
            }
            else if (fragmentMaterial == null)
            {
                Debug.LogWarning("[Brick] fragmentMaterial 未赋值，无法生成碎片");
            }

            // PARTICLE BLOCK SHATTER：砖色小方块从球位置炸开
            if (shatterParticleCount > 0)
            {
                float baseAngleDeg = -Mathf.Atan2(ballWorldVel.x, ballWorldVel.y) * Mathf.Rad2Deg;
                SpawnBurst(ballWorldPos, shatterParticleCount, 45f, baseAngleDeg,
                    shatterParticleSpeed, 0.5f, brickColor, 0.3f, 0.6f, particleSize);
            }

            gameObject.SetActive(false);
        }

        private void SpawnSlice(List<Vector2> verts, List<Vector2> uvs, Sprite sprite,
            Color color, Vector2 vel, float angVel)
        {
            var go = new GameObject("BrickFragment");
            go.transform.position = _visual.position;
            go.transform.rotation = _visual.rotation;
            go.transform.localScale = _visual.lossyScale;
            go.transform.SetParent(Container, true);

            // 多边形 Mesh
            var mesh = new Mesh { name = "BrickFragmentMesh" };
            var v3 = new Vector3[verts.Count];
            for (int i = 0; i < verts.Count; i++) v3[i] = verts[i];
            mesh.SetVertices(v3);
            mesh.SetUVs(0, uvs.ToArray());
            mesh.SetTriangles(TriangulateFan(verts.Count), 0);
            mesh.RecalculateBounds();

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var mr = go.AddComponent<MeshRenderer>();
            // 材质实例（FragmentUnlit2D shader，兼容 URP 2D）
            var mat = new Material(fragmentMaterial);
            mat.SetTexture(MainTexId, sprite.texture);
            mat.SetColor(ColorId, color);
            mr.sharedMaterial = mat;
            mr.sortingOrder = 100;

            AnimateFragmentAsync(go, vel, angVel, _visual.position, go.transform.localScale,
                go.GetCancellationTokenOnDestroy()).Forget();
        }

        // 碎片物理：PUSH + GRAVITY + DAMPING + ROTATE + SCALE
        private async UniTaskVoid AnimateFragmentAsync(GameObject go, Vector2 vel, float angVel,
            Vector3 baseWorldPos, Vector3 initialScale, CancellationToken ct)
        {
            Vector2 pos = Vector2.zero;
            float age = 0f;
            while (age < fragLifetime)
            {
                float dt = Time.deltaTime;
                age += dt;
                pos += vel * dt;
                vel.y -= fragGravity * dt;
                vel *= Mathf.Max(0f, 1f - fragDamping * dt);
                angVel *= Mathf.Max(0f, 1f - fragDamping * dt);
                go.transform.position = baseWorldPos + new Vector3(pos.x, pos.y, 0f);
                go.transform.Rotate(0f, 0f, angVel * dt);
                // SCALE：(1-t)² 缩放到 0（juicy GTween easeOutQuad）
                float st = Mathf.Clamp01(age / fragLifetime);
                go.transform.localScale = initialScale * ((1f - st) * (1f - st));
                await UniTask.Yield(ct);
            }

            if (go != null)
            {
                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null && mr.sharedMaterial != null) Destroy(mr.sharedMaterial);
                Destroy(go);
            }
        }

        #endregion

        #region 粒子（静态共享方法，Ball/Paddle 也调用）

        public static void SpawnBurst(Vector2 pos, int count, float spread, float baseAngleDeg,
            float speed, float speedVariance, Color color, float minLife, float maxLife, float size)
        {
            for (int i = 0; i < count; i++)
            {
                float spreadRnd = UnityEngine.Random.value * spread - spread * 0.5f;
                float speedRnd = UnityEngine.Random.value * speedVariance - speedVariance * 0.5f;
                float angleRad = (-baseAngleDeg + spreadRnd) * Mathf.Deg2Rad;
                float s = speed * (1f + speedRnd);
                Vector2 disp = new Vector2(Mathf.Sin(angleRad), Mathf.Cos(angleRad)) * s;
                float life = minLife + UnityEngine.Random.value * (maxLife - minLife);
                float shade = 0.8f + UnityEngine.Random.value * 0.2f;
                SpawnParticle(pos, disp, color * shade, life, size);
            }
        }

        private static void SpawnParticle(Vector2 pos, Vector2 disp, Color color, float life, float size)
        {
            var go = new GameObject("Particle");
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * size;
            go.transform.SetParent(Container, true);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SquareSprite;
            sr.color = color;
            sr.sortingOrder = 100;

            AnimateParticleAsync(go, pos, disp, life, size, go.GetCancellationTokenOnDestroy()).Forget();
        }

        private static async UniTaskVoid AnimateParticleAsync(GameObject go, Vector2 spawn, Vector2 disp,
            float life, float baseSize, CancellationToken ct)
        {
            Vector3 start = spawn;
            Vector3 end = spawn + disp;
            float elapsed = 0f;
            while (elapsed < life)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / life);
                float e = 1f - (1f - t) * (1f - t);
                go.transform.position = Vector3.LerpUnclamped(start, end, e);
                go.transform.localScale = Vector3.one * (baseSize * Mathf.LerpUnclamped(1f, 0.1f, e));
                await UniTask.Yield(ct);
            }
            if (go != null) Destroy(go);
        }

        #endregion

        #region 彩纸（物理动画：重力弧线 + 旋转 + 飘荡，用于 PADDLE_COLLISION）

        public static void SpawnConfetti(Vector2 pos, int count, Color[] colors,
            float minUpward, float maxUpward, float spread, float gravity,
            float minLife, float maxLife, float minSize, float maxSize)
        {
            for (int i = 0; i < count; i++)
            {
                Color c = colors[UnityEngine.Random.Range(0, colors.Length)];
                float shade = 0.85f + UnityEngine.Random.value * 0.15f;
                c *= shade;

                float upSpeed = UnityEngine.Random.Range(minUpward, maxUpward);
                float horizSpeed = (UnityEngine.Random.value - 0.5f) * 2f * spread;
                Vector2 vel = new Vector2(horizSpeed, upSpeed);

                float angVel = UnityEngine.Random.Range(-540f, 540f);
                float life = UnityEngine.Random.Range(minLife, maxLife);
                float size = UnityEngine.Random.Range(minSize, maxSize);
                float flutterFreq = UnityEngine.Random.Range(4f, 8f);
                float flutterAmp = UnityEngine.Random.Range(0.5f, 1.5f);

                SpawnConfettiParticle(pos, vel, angVel, gravity, life, size, c, flutterFreq, flutterAmp);
            }
        }

        private static void SpawnConfettiParticle(Vector2 pos, Vector2 vel, float angVel,
            float gravity, float life, float size, Color color, float flutterFreq, float flutterAmp)
        {
            var go = new GameObject("Confetti");
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * size;
            go.transform.SetParent(Container, true);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SquareSprite;
            sr.color = color;
            sr.sortingOrder = 100;

            AnimateConfettiAsync(go, pos, vel, angVel, gravity, life, size,
                flutterFreq, flutterAmp, go.GetCancellationTokenOnDestroy()).Forget();
        }

        private static async UniTaskVoid AnimateConfettiAsync(GameObject go, Vector2 startPos,
            Vector2 vel, float angVel, float gravity, float life, float baseSize,
            float flutterFreq, float flutterAmp, CancellationToken ct)
        {
            Vector2 pos = startPos;
            float age = 0f;
            while (age < life)
            {
                float dt = Time.deltaTime;
                age += dt;
                vel.y -= gravity * dt;
                vel.x += Mathf.Sin(age * flutterFreq) * flutterAmp * dt;
                pos += vel * dt;
                go.transform.position = pos;
                go.transform.Rotate(0f, 0f, angVel * dt);
                float t = age / life;
                float scl = t > 0.7f ? Mathf.Lerp(1f, 0f, (t - 0.7f) / 0.3f) : 1f;
                go.transform.localScale = Vector3.one * (baseSize * scl);
                await UniTask.Yield(ct);
            }
            if (go != null) Destroy(go);
        }

        #endregion

        #region 碎片容器（重置时批量清理）

        private static Transform Container
        {
            get
            {
                if (_container == null)
                    _container = new GameObject("BrickFragments").transform;
                return _container;
            }
        }

        public static void ClearFragments()
        {
            if (_container == null) return;
            for (int i = _container.childCount - 1; i >= 0; i--)
            {
                var child = _container.GetChild(i);
                var mr = child.GetComponent<MeshRenderer>();
                if (mr != null && mr.sharedMaterial != null) Destroy(mr.sharedMaterial);
                Destroy(child.gameObject);
            }
        }

        private static Sprite SquareSprite
        {
            get
            {
                if (_squareSprite != null && _squareSprite.texture != null) return _squareSprite;
                var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var px = new Color[32 * 32];
                for (int i = 0; i < px.Length; i++) px[i] = Color.white;
                tex.SetPixels(px);
                tex.Apply();
                _squareSprite = Sprite.Create(tex, new Rect(0f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f), 32f);
                return _squareSprite;
            }
        }

        #endregion

        #region Sprite 外接矩形 + UV

        private static void GetRectFromSprite(Sprite sprite, out Vector2[] verts, out Vector2[] uvs)
        {
            Vector2 half = sprite.bounds.size * 0.5f;
            verts = new Vector2[]
            {
                new Vector2(-half.x, -half.y),
                new Vector2( half.x, -half.y),
                new Vector2( half.x,  half.y),
                new Vector2(-half.x,  half.y),
            };
            Rect r = sprite.textureRect;
            Vector2 ts = new Vector2(sprite.texture.width, sprite.texture.height);
            uvs = new Vector2[]
            {
                new Vector2(r.xMin / ts.x, r.yMin / ts.y),
                new Vector2(r.xMax / ts.x, r.yMin / ts.y),
                new Vector2(r.xMax / ts.x, r.yMax / ts.y),
                new Vector2(r.xMin / ts.x, r.yMax / ts.y),
            };
        }

        #endregion

        #region 切片几何（移植自 juicy LineSliceObject）

        private static bool Slice(IList<Vector2> verts, IList<Vector2> uvs, Vector2 p1, Vector2 p2,
            out List<Vector2> ov1, out List<Vector2> ou1, out List<Vector2> ov2, out List<Vector2> ou2)
        {
            ov1 = ou1 = ov2 = ou2 = null;
            var aV = new List<Vector2>(); var aU = new List<Vector2>();
            var bV = new List<Vector2>(); var bU = new List<Vector2>();
            var curV = aV; var curU = aU;
            int crosses = 0;
            int n = verts.Count;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                curV.Add(verts[i]); curU.Add(uvs[i]);
                if (SegIntersect(verts[i], verts[j], p1, p2, out Vector2 hit, out float t))
                {
                    Vector2 hitUv = Vector2.Lerp(uvs[i], uvs[j], t);
                    curV.Add(hit); curU.Add(hitUv);
                    curV = (curV == aV) ? bV : aV;
                    curU = (curU == aU) ? bU : aU;
                    curV.Add(hit); curU.Add(hitUv);
                    crosses++;
                }
            }
            if (crosses != 2) return false;
            ov1 = aV; ou1 = aU; ov2 = bV; ou2 = bU;
            return true;
        }

        private static bool SegIntersect(Vector2 a, Vector2 b, Vector2 p1, Vector2 p2, out Vector2 hit, out float t)
        {
            hit = Vector2.zero; t = 0f;
            Vector2 d1 = b - a;
            Vector2 d2 = p2 - p1;
            float denom = d1.x * d2.y - d1.y * d2.x;
            if (Mathf.Abs(denom) < 1e-6f) return false;
            Vector2 diff = p1 - a;
            t = (diff.x * d2.y - diff.y * d2.x) / denom;
            float s = (diff.x * d1.y - diff.y * d1.x) / denom;
            if (t < 0f || t > 1f || s < 0f || s > 1f) return false;
            hit = a + d1 * t;
            return true;
        }

        private static int[] TriangulateFan(int vertexCount)
        {
            if (vertexCount < 3) return Array.Empty<int>();
            int[] tris = new int[(vertexCount - 2) * 3];
            int idx = 0;
            for (int i = 1; i < vertexCount - 1; i++)
            {
                tris[idx++] = 0;
                tris[idx++] = i;
                tris[idx++] = i + 1;
            }
            return tris;
        }

        #endregion

        #region 缓动

        private static float EaseOutBack(float k)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(k - 1f, 3f) + c1 * Mathf.Pow(k - 1f, 2f);
        }

        #endregion
    }
}
