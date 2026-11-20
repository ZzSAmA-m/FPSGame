using System;
using System.Linq;
using System.Reflection;
using System.Text;
using GamePlay.Player;
using GamePlay.Player.Combat;
using Network.Server.Combat;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPSGame.Editor.Validation
{
    /// <summary>隔离验证历史命中形状、历史连续性和静态世界遮挡。</summary>
    public static class CombatGeometryValidation
    {
        private static bool _running;

        [CliCommand("combat_geometry_validate", "Validate sphere/capsule hit geometry, rewind history and wall occlusion", Tags = new[] { "fpsgame/validation" })]
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return "REFUSED 请先退出 Play Mode，再运行隔离命中几何验证。";
            if (_running)
                return "REFUSED 命中几何验证正在运行。";
            _running = true;
            var report = new StringBuilder();
            int passed = 0;
            try
            {
                RunCase("球体最近交点、背向和最大射程", TestSphere, report, ref passed);
                RunCase("球体切线和射线起点位于体内", TestSphereBoundary, report, ref passed);
                RunCase("胶囊侧面、端球和沿轴方向求交", TestCapsule, report, ref passed);
                RunCase("胶囊切线、体内、范围及倾斜轴", TestCapsuleBoundary, report, ref passed);
                RunCase("胶囊退化为球体及插值端点重合", TestDegenerateCapsule, report, ref passed);
                RunCase("非法形状、方向和射程被拒绝", TestInvalidGeometry, report, ref passed);
                RunCase("Collider 世界几何正确应用轴向、缩放与旋转", TestColliderCapture, report, ref passed);
                RunCase("64 帧历史淘汰最早记录并区分出生前和缓存过旧", TestHistoryCapacity, report, ref passed);
                RunCase("分数 Tick 插值头部、胶囊端点及半径", TestHistoryInterpolation, report, ref passed);
                RunCase("单帧精确查询有效，未知玩家与未来时间拒绝", TestMissingHistory, report, ref passed);
                RunCase("死亡、重生隔离旧生命，传送连续段不被跨越", TestHistoryLifecycle, report, ref passed);
                RunCase("历史拒绝非法与逆序帧，移除及清空生效", TestHistoryValidation, report, ref passed);
                RunCase("解析器排除射手并选择最近目标", TestResolverTargets, report, ref passed);
                RunCase("头身 1 mm 边界按头部归类且保持最近目标排序", TestHeadBodyTie, report, ref passed);
                RunCase("解析器区分头身并限制最大射程", TestResolverRangeAndRegion, report, ref passed);
                RunCase("静态墙体遮挡历史目标，前方目标仍可命中", TestMapOcclusion, report, ref passed);
                RunCase("前移 0.3 m 内近墙截断射击", TestNearWall, report, ref passed);
                RunCase("地图遮挡过滤玩家层级下的 CharacterController", TestMovementColliderFilter, report, ref passed);
                RunCase("玩家预制体显式引用头球体和身体胶囊 Trigger", TestPrefabBindings, report, ref passed);
                return "PASS " + passed + " checks\n" + report;
            }
            finally { _running = false; }
        }

        private static void TestSphere()
        {
            var sphere = new HitSphere(new Vector3(0f, 0f, 5f), 1f);
            Require(HitboxRayMath.RaycastSphere(new Ray(Vector3.zero, Vector3.forward), sphere, 10f, out float distance),
                "正向射线应击中球体。");
            Near(distance, 4f, "球体应返回近表面交点。");
            Require(!HitboxRayMath.RaycastSphere(new Ray(Vector3.zero, Vector3.back), sphere, 10f, out _) &&
                    !HitboxRayMath.RaycastSphere(new Ray(Vector3.zero, Vector3.forward), sphere, 3.99f, out _) &&
                    HitboxRayMath.RaycastSphere(new Ray(Vector3.zero, Vector3.forward), sphere, 4f, out _),
                "背向射线和范围外球体必须拒绝，精确射程边界可命中。");
        }

        private static void TestSphereBoundary()
        {
            var sphere = new HitSphere(new Vector3(0f, 0f, 5f), 1f);
            Require(HitboxRayMath.RaycastSphere(new Ray(new Vector3(1f, 0f, 0f), Vector3.forward), sphere, 10f,
                out float tangent), "球体切线应形成有效交点。");
            Near(tangent, 5f, "切线应在球心所在深度相交。");
            Require(HitboxRayMath.RaycastSphere(new Ray(sphere.Center, Vector3.forward), sphere, 0f, out float inside),
                "从球体内部开始的射线应返回即时命中。");
            Near(inside, 0f, "体内射线交点距离应为零。");
        }

        private static void TestCapsule()
        {
            var upright = new HitCapsule(new Vector3(0f, -1f, 5f), new Vector3(0f, 1f, 5f), 0.5f);
            Require(HitboxRayMath.RaycastCapsule(new Ray(Vector3.zero, Vector3.forward), upright, 10f, out float side),
                "射线应命中胶囊圆柱侧面。");
            Near(side, 4.5f, "胶囊侧面最近交点错误。");
            Require(HitboxRayMath.RaycastCapsule(new Ray(new Vector3(0f, 3f, 5f), Vector3.down), upright, 10f,
                out float cap), "从轴向接近时应命中端球。");
            Near(cap, 1.5f, "胶囊上端球距离错误。");
            var forward = new HitCapsule(new Vector3(0f, 0f, 4f), new Vector3(0f, 0f, 6f), 0.5f);
            Require(HitboxRayMath.RaycastCapsule(new Ray(Vector3.zero, Vector3.forward), forward, 10f, out float parallel),
                "平行于胶囊轴的射线应通过端球求交。");
            Near(parallel, 3.5f, "轴向胶囊最近交点错误。");
        }

        private static void TestCapsuleBoundary()
        {
            var capsule = new HitCapsule(new Vector3(0f, -1f, 5f), new Vector3(0f, 1f, 5f), 0.5f);
            Require(HitboxRayMath.RaycastCapsule(new Ray(new Vector3(0.5f, 0f, 0f), Vector3.forward), capsule, 10f,
                out float tangent), "胶囊圆柱侧面的切线应有效。");
            Near(tangent, 5f, "胶囊切线距离错误。");
            Require(HitboxRayMath.RaycastCapsule(new Ray(new Vector3(0f, 0f, 5f), Vector3.right), capsule, 0f,
                    out float inside) && inside == 0f &&
                    !HitboxRayMath.RaycastCapsule(new Ray(Vector3.zero, Vector3.forward), capsule, 4.49f, out _) &&
                    HitboxRayMath.RaycastCapsule(new Ray(Vector3.zero, Vector3.forward), capsule, 4.5f, out _),
                "胶囊体内与射程边界应稳定处理。");
            Vector3 axis = new Vector3(1f, 1f, 0f).normalized;
            var tilted = new HitCapsule(new Vector3(0f, 0f, 5f) - axis, new Vector3(0f, 0f, 5f) + axis, 0.4f);
            Require(HitboxRayMath.RaycastCapsule(new Ray(Vector3.zero, Vector3.forward), tilted, 10f, out float distance),
                "倾斜胶囊轴应正确求交。");
            Near(distance, 4.6f, "倾斜胶囊最近距离错误。");
        }

        private static void TestDegenerateCapsule()
        {
            var point = new Vector3(0f, 0f, 5f);
            var capsule = new HitCapsule(point, point, 1f);
            Require(HitboxRayMath.RaycastCapsule(new Ray(Vector3.zero, Vector3.forward), capsule, 10f, out float distance),
                "重合端点必须退化为可查询的球体。");
            Near(distance, 4f, "退化球体距离错误。");
            var from = new HitCapsule(point + Vector3.up, point - Vector3.up, 0.5f);
            var to = new HitCapsule(point - Vector3.up, point + Vector3.up, 0.5f);
            HitCapsule interpolated = HitCapsule.Interpolate(from, to, 0.5f);
            bool intersects = HitboxRayMath.RaycastCapsule(new Ray(Vector3.zero, Vector3.forward), interpolated,
                10f, out float middle);
            Require(interpolated.PointA == interpolated.PointB && intersects,
                "端点插值恰好重合时仍必须能求交。");
            Near(middle, 4.5f, "插值退化球体距离错误。");
        }

        private static void TestInvalidGeometry()
        {
            Ray forward = new(Vector3.zero, Vector3.forward);
            Require(!HitboxRayMath.RaycastSphere(forward, new HitSphere(Vector3.zero, 0f), 10f, out _) &&
                    !HitboxRayMath.RaycastSphere(forward, new HitSphere(new Vector3(float.NaN, 0f, 5f), 1f), 10f, out _) &&
                    !HitboxRayMath.RaycastCapsule(forward, new HitCapsule(Vector3.zero, Vector3.up, -1f), 10f, out _) &&
                    !HitboxRayMath.RaycastSphere(new Ray(Vector3.zero, Vector3.zero), new HitSphere(Vector3.forward, 1f), 10f, out _) &&
                    !HitboxRayMath.RaycastSphere(forward, new HitSphere(Vector3.forward, 1f), -1f, out _),
                "零半径、非有限坐标、负半径、零方向和负射程必须被拒绝。");
        }

        private static void TestColliderCapture()
        {
            using var scope = new PhysicsScope();
            GameObject root = scope.CreateRoot("Capture");
            PlayerHitboxController controller = root.AddComponent<PlayerHitboxController>();
            GameObject headRoot = scope.CreateRoot("Head");
            headRoot.transform.SetParent(root.transform, false);
            headRoot.transform.localScale = new Vector3(2f, 3f, 4f);
            SphereCollider head = headRoot.AddComponent<SphereCollider>();
            head.center = new Vector3(1f, 0f, 0f);
            head.radius = 0.25f;
            GameObject bodyRoot = scope.CreateRoot("Body");
            bodyRoot.transform.SetParent(root.transform, false);
            bodyRoot.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            bodyRoot.transform.localScale = new Vector3(2f, 3f, 4f);
            CapsuleCollider body = bodyRoot.AddComponent<CapsuleCollider>();
            body.direction = 0;
            body.height = 4f;
            body.radius = 0.25f;
            Set(controller, "_head", head);
            Set(controller, "_body", body);
            PlayerHitboxSnapshot snapshot = controller.Capture();
            Near(snapshot.Head.Radius, 1f, "头部球体应按最大世界缩放计算半径。");
            VectorNear(snapshot.Head.Center, head.transform.TransformPoint(head.center), "头部中心必须使用 Collider 的局部中心。");
            Near(snapshot.Body.Radius, 1f, "X 轴胶囊应取 Y/Z 最大缩放计算半径。");
            Near(Vector3.Distance(snapshot.Body.PointA, snapshot.Body.PointB), 6f, "胶囊端点间距应为世界高度减两倍半径。");
            Require(Vector3.Dot((snapshot.Body.PointB - snapshot.Body.PointA).normalized, Vector3.up) > 0.999f,
                "胶囊端点顺序应随局部 Collider 轴旋转到世界空间。");
        }

        private static void TestHistoryCapacity()
        {
            var history = new ServerHitboxHistory();
            for (int tick = 1; tick <= 70; tick++) history.Record(Frame(1, tick, Geometry(tick)));
            Require(history.Capacity == 64 && history.TrySample(1, 7d, out _, out _) &&
                    history.TrySample(1, 70d, out _, out _), "64 帧窗口应保留第 7 至 70 帧。");
            Require(!history.TrySample(1, 6d, out _, out HitboxHistoryFailure old) && old == HitboxHistoryFailure.TooOld &&
                    !history.TrySample(1, 0d, out _, out HitboxHistoryFailure unborn) && unborn == HitboxHistoryFailure.NotYetBorn,
                "缓存淘汰与玩家尚未出生必须有不同失败原因。");
        }

        private static void TestHistoryInterpolation()
        {
            var history = new ServerHitboxHistory();
            var from = new PlayerHitboxSnapshot(new HitSphere(Vector3.zero, 1f),
                new HitCapsule(new Vector3(0f, -1f, 0f), new Vector3(0f, 1f, 0f), 0.5f));
            var to = new PlayerHitboxSnapshot(new HitSphere(new Vector3(4f, 2f, 6f), 3f),
                new HitCapsule(new Vector3(2f, -2f, 8f), new Vector3(2f, 2f, 8f), 1.5f));
            history.Record(Frame(1, 10d, from));
            history.Record(Frame(1, 12d, to));
            Require(history.TrySample(1, 11d, out ServerHitboxFrame middle, out _), "历史应支持两个服务器帧之间的小数查询。");
            VectorNear(middle.Geometry.Head.Center, new Vector3(2f, 1f, 3f), "头部中心插值错误。");
            Near(middle.Geometry.Head.Radius, 2f, "头部半径插值错误。");
            VectorNear(middle.Geometry.Body.PointA, new Vector3(1f, -1.5f, 4f), "胶囊 A 端点插值错误。");
            VectorNear(middle.Geometry.Body.PointB, new Vector3(1f, 1.5f, 4f), "胶囊 B 端点插值错误。");
            Near(middle.Geometry.Body.Radius, 1f, "胶囊半径插值错误。");
        }

        private static void TestMissingHistory()
        {
            var history = new ServerHitboxHistory();
            Require(!history.TrySample(999, 1d, out _, out HitboxHistoryFailure missing) &&
                    missing == HitboxHistoryFailure.UnknownPlayer, "未知目标不能产生历史几何。");
            history.Record(Frame(1, 10d, Geometry(5f)));
            Require(history.TrySample(1, 10d, out _, out _) &&
                    !history.TrySample(1, 10.5d, out _, out HitboxHistoryFailure future) &&
                    future == HitboxHistoryFailure.NewerThanHistory &&
                    !history.TrySample(1, double.NaN, out _, out HitboxHistoryFailure invalid) &&
                    invalid == HitboxHistoryFailure.InvalidTime,
                "单帧精确命中可用，历史不应外推或接受非有限查询时间。");
        }

        private static void TestHistoryLifecycle()
        {
            var history = new ServerHitboxHistory();
            history.Record(Frame(1, 1d, Geometry(5f)));
            history.Record(Frame(1, 2d, Geometry(5f), continuity: 2, alive: false));
            Require(!history.TrySample(1, 1d, out _, out HitboxHistoryFailure dead) && dead == HitboxHistoryFailure.Dead,
                "当前已经死亡的目标不能被旧几何继续选为有效伤害目标。");
            history.Record(Frame(1, 3d, Geometry(8f), life: 2, continuity: 3));
            Require(!history.TrySample(1, 1d, out _, out HitboxHistoryFailure previousLife) &&
                    previousLife == HitboxHistoryFailure.LifeChanged && history.TrySample(1, 3d, out _, out _),
                "重生后应拒绝旧生命历史并接受当前生命的精确帧。");
            history.Record(Frame(1, 4d, Geometry(100f), life: 2, continuity: 4));
            Require(!history.TrySample(1, 3.5d, out _, out HitboxHistoryFailure discontinuity) &&
                    discontinuity == HitboxHistoryFailure.Discontinuous,
                "传送前后不能构造不存在的中间碰撞位置。");
        }

        private static void TestHistoryValidation()
        {
            var history = new ServerHitboxHistory();
            history.Record(Frame(1, 2d, Geometry(5f)));
            Expect<ArgumentException>(() => history.Record(Frame(1, 1d, Geometry(5f))), "逆序帧应被拒绝。");
            Expect<ArgumentException>(() => history.Record(Frame(0, 3d, Geometry(5f))), "零玩家身份应被拒绝。");
            Expect<ArgumentException>(() => history.Record(Frame(1, double.PositiveInfinity, Geometry(5f))), "非有限帧时间应被拒绝。");
            Require(history.Remove(1) && !history.PlayerIds.Any(), "移除断线玩家应删除其历史集合。");
            history.Record(Frame(2, 1d, Geometry(5f)));
            history.Clear();
            Require(!history.PlayerIds.Any(), "服务端关闭后不应保留历史目标。");
        }

        private static void TestResolverTargets()
        {
            var history = new ServerHitboxHistory();
            history.Record(Frame(1, 10d, Geometry(1f)));
            history.Record(Frame(2, 10d, Geometry(7f)));
            history.Record(Frame(3, 10d, Geometry(5f)));
            var resolver = PureResolver();
            ServerShotResult hit = resolver.Resolve(history, 1, ForwardRay(), ForwardRay(), 10d);
            Require(hit.Status == ServerShotStatus.Hit && hit.TargetId == 3 && hit.Region == HitRegion.Body,
                "射手本身必须排除，历史目标应按最近交点选择。");
            Near(hit.Distance, 4.5f, "最近目标交点距离错误。");
            history.Remove(3);
            Require(resolver.Resolve(history, 1, ForwardRay(), ForwardRay(), 10d).TargetId == 2,
                "目标移除后应能命中后方仍存在的目标。");
        }

        private static void TestHeadBodyTie()
        {
            var history = new ServerHitboxHistory();
            var tie = new PlayerHitboxSnapshot(new HitSphere(new Vector3(0f, 0f, 5f), 1f),
                new HitCapsule(new Vector3(0f, 0f, 4.9995f), new Vector3(0f, 0f, 4.9995f), 1f));
            history.Record(Frame(2, 10d, tie));
            var rear = new PlayerHitboxSnapshot(new HitSphere(new Vector3(0f, 10f, 5f), 0.1f),
                new HitCapsule(new Vector3(0f, 0f, 4.99975f), new Vector3(0f, 0f, 4.99975f), 1f));
            history.Record(Frame(3, 10d, rear));
            ServerShotResult hit = PureResolver().Resolve(history, 1, ForwardRay(), ForwardRay(), 10d);
            Require(hit.TargetId == 2 && hit.Region == HitRegion.Head,
                "头身容差只能改变区域归类，不能把实际更近的目标排到后面。");
            Near(hit.Distance, 3.9995f, "最近排序应保留真实较近的交点距离。");
        }

        private static void TestResolverRangeAndRegion()
        {
            var history = new ServerHitboxHistory();
            history.Record(Frame(2, 10d, Geometry(5f)));
            var headRay = new Ray(new Vector3(0f, 2f, 0f), Vector3.forward);
            Require(PureResolver().Resolve(history, 1, headRay, headRay, 10d).Region == HitRegion.Head,
                "头部球体应归类为 Head。");
            history.Clear();
            history.Record(Frame(2, 10d, Geometry(201f)));
            Require(PureResolver().Resolve(history, 1, ForwardRay(), ForwardRay(), 10d).Status == ServerShotStatus.Miss,
                "200 米外的首次交点不能命中。");
            history.Record(Frame(2, 11d, Geometry(200.5f)));
            ServerShotResult boundary = PureResolver().Resolve(history, 1, ForwardRay(), ForwardRay(), 11d);
            Require(boundary.Status == ServerShotStatus.Hit, "精确 200 米的交点应可命中。");
            Near(boundary.Distance, 200f, "最大射程交点距离错误。");
        }

        private static void TestMapOcclusion()
        {
            using var scope = new PhysicsScope();
            scope.Wall(3f);
            Physics.SyncTransforms();
            var history = new ServerHitboxHistory();
            history.Record(Frame(2, 10d, Geometry(5f, PhysicsScope.Origin)));
            var ray = new Ray(PhysicsScope.Origin, Vector3.forward);
            var resolver = new ServerShotResolver(new ServerCombatSettings { StaticCollisionMask = 1 << 31 });
            Require(resolver.Resolve(history, 1, ray, ray, 10d).Status == ServerShotStatus.MapBlocked,
                "历史目标后方的射线应被其前方静态墙体截断。");
            history.Record(Frame(2, 11d, Geometry(2f, PhysicsScope.Origin)));
            Require(resolver.Resolve(history, 1, ray, ray, 11d).Status == ServerShotStatus.Hit,
                "墙前的目标应优先于更远的地图遮挡。");
        }

        private static void TestNearWall()
        {
            using var scope = new PhysicsScope();
            scope.Wall(0.15f);
            Physics.SyncTransforms();
            var history = new ServerHitboxHistory();
            history.Record(Frame(2, 10d, Geometry(5f, PhysicsScope.Origin)));
            var eye = new Ray(PhysicsScope.Origin, Vector3.forward);
            var shot = new Ray(PhysicsScope.Origin + Vector3.forward * 0.3f, Vector3.forward);
            var resolver = new ServerShotResolver(new ServerCombatSettings { StaticCollisionMask = 1 << 31 });
            ServerShotResult blocked = resolver.Resolve(history, 1, eye, shot, 10d);
            Require(blocked.Status == ServerShotStatus.MapBlocked && blocked.Distance < 0.3f,
                "位于眼点与前移射击原点之间的墙不能被跳过。");
        }

        private static void TestMovementColliderFilter()
        {
            using var scope = new PhysicsScope();
            GameObject player = scope.CreateRoot("MovementCollider");
            player.transform.position = PhysicsScope.Origin + Vector3.forward * 2f;
            player.AddComponent<PlayerController>();
            CharacterController movement = player.AddComponent<CharacterController>();
            movement.height = 2f;
            movement.radius = 0.5f;
            player.SetActive(true);
            Physics.SyncTransforms();
            var history = new ServerHitboxHistory();
            history.Record(Frame(2, 10d, Geometry(5f, PhysicsScope.Origin)));
            var ray = new Ray(PhysicsScope.Origin, Vector3.forward);
            var resolver = new ServerShotResolver(new ServerCombatSettings { StaticCollisionMask = 1 << 31 });
            Require(resolver.Resolve(history, 1, ray, ray, 10d).Status == ServerShotStatus.Hit,
                "玩家 CharacterController 不能冒充静态墙体阻挡历史几何查询。");
        }

        private static void TestPrefabBindings()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
            Require(prefab != null, "真实玩家预制体必须存在。");
            PlayerHitboxController controller = prefab.GetComponent<PlayerHitboxController>();
            Require(controller != null && controller.HeadCollider != null && controller.BodyCollider != null,
                "玩家预制体必须挂载控制器并显式绑定两个 Collider。");
            Require(controller.HeadCollider.isTrigger && controller.BodyCollider.isTrigger &&
                    controller.BodyCollider.direction == 1 && controller.Capture().IsValid,
                "头部球体与 Y 轴身体胶囊必须是有效 Trigger 几何。");
        }

        private static PlayerHitboxSnapshot Geometry(float z, Vector3 offset = default) => new(
            new HitSphere(offset + new Vector3(0f, 2f, z), 0.25f),
            new HitCapsule(offset + new Vector3(0f, -0.5f, z), offset + new Vector3(0f, 0.5f, z), 0.5f));

        private static ServerHitboxFrame Frame(uint id, double tick, PlayerHitboxSnapshot geometry,
            uint life = 1, uint continuity = 1, bool alive = true) => new(id, tick, life, continuity, alive, geometry);

        private static Ray ForwardRay() => new(Vector3.zero, Vector3.forward);
        private static ServerShotResolver PureResolver() => new(new ServerCombatSettings { StaticCollisionMask = 0 });

        private static void Set(object target, string field, object value) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static void Near(float actual, float expected, string message)
        {
            Require(Mathf.Abs(actual - expected) <= 0.001f, message + " actual=" + actual + " expected=" + expected);
        }

        private static void VectorNear(Vector3 actual, Vector3 expected, string message)
        {
            Require(Vector3.Distance(actual, expected) <= 0.001f, message);
        }

        private static void Expect<T>(Action action, string message) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new InvalidOperationException(message);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void RunCase(string name, Action test, StringBuilder report, ref int passed)
        {
            try { test(); }
            catch (Exception exception) { throw new InvalidOperationException("命中几何验证失败 " + name, exception); }
            passed++;
            report.AppendLine("PASS " + name);
        }

        private sealed class PhysicsScope : IDisposable
        {
            public static readonly Vector3 Origin = new(0f, 10000f, 0f);
            private readonly Scene _previousActiveScene = SceneManager.GetActiveScene();
            private readonly Scene _scene;

            public PhysicsScope()
            {
                // 默认物理场景中的临时 additive scene 供真实 Physics.RaycastAll 查询。
                _scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(_previousActiveScene);
            }

            public GameObject CreateRoot(string name)
            {
                GameObject root = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave);
                root.SetActive(false);
                root.layer = 31;
                SceneManager.MoveGameObjectToScene(root, _scene);
                return root;
            }

            public void Wall(float z)
            {
                GameObject wall = CreateRoot("CombatValidationWall");
                wall.transform.position = Origin + Vector3.forward * z;
                BoxCollider collider = wall.AddComponent<BoxCollider>();
                collider.size = new Vector3(4f, 4f, 0.02f);
                wall.SetActive(true);
            }

            public void Dispose()
            {
                if (_previousActiveScene.IsValid() && _previousActiveScene.isLoaded)
                    SceneManager.SetActiveScene(_previousActiveScene);
                if (_scene.IsValid()) EditorSceneManager.CloseScene(_scene, true);
                Physics.SyncTransforms();
            }
        }
    }
}
