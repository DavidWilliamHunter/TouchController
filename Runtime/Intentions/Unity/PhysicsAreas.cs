using System;
using UnityEngine;

namespace Threads.TouchController.Intentions
{
    /// <summary>Which physics worlds a <see cref="PhysicsRaycastArea"/> looks in.</summary>
    [Flags]
    public enum PhysicsWorld
    {
        /// <summary>3D colliders (<c>Physics</c>).</summary>
        Physics3D = 1,

        /// <summary>2D colliders (<c>Physics2D</c>).</summary>
        Physics2D = 2,

        /// <summary>Both: the hit nearest the camera wins (default).</summary>
        Both = Physics3D | Physics2D,
    }

    /// <summary>What a physics area hit. Reported as <see cref="IntentionEventData.Target"/>.</summary>
    public sealed class WorldHit
    {
        public WorldHit(GameObject gameObject, Component component, Vector3 point, Vector3 normal, float distance, bool is2D)
        {
            GameObject = gameObject;
            Component = component;
            Point = point;
            Normal = normal;
            Distance = distance;
            Is2D = is2D;
        }

        public GameObject GameObject { get; }

        /// <summary>The component asked for (or the collider, if none was).</summary>
        public Component Component { get; }

        /// <summary>World-space point where the ray hit (for 2D, on the collider's z plane).</summary>
        public Vector3 Point { get; }

        public Vector3 Normal { get; }

        /// <summary>Distance along the ray, which starts on the camera's near clip plane (the same for 3D and 2D hits).</summary>
        public float Distance { get; }

        /// <summary>True for a 2D collider.</summary>
        public bool Is2D { get; }

        public override string ToString() => $"{GameObject?.name} @ {Point}{(Is2D ? " (2D)" : "")}";
    }

    /// <summary>
    /// What is under the finger in the scene: a ray from the camera through the finger, tested against 3D
    /// and/or 2D colliders. With both (the default), the hit nearest the camera wins, so scenes that mix
    /// 2D sprites and 3D objects just work. Target: a <see cref="WorldHit"/>.
    /// </summary>
    /// <remarks>
    /// The 3D and 2D halves compile only when Unity's Physics / Physics 2D modules are installed; a world
    /// whose module is missing is skipped, so code using this class compiles in any project. With a
    /// component type, the collider must belong to (be inside) such a component, which is reported.
    /// 2D trigger hits follow the project's <c>Physics2D.queriesHitTriggers</c> setting.
    /// </remarks>
    public sealed class PhysicsRaycastArea : IIntentionArea
    {
        private readonly Camera _camera;
        private readonly PhysicsWorld _worlds;
        private readonly LayerMask _layers;
        private readonly float _maxDistance;
        private readonly Type _component;
        private readonly bool? _hitTriggers;

        /// <summary>Every layer except Ignore Raycast (Unity's default for 3D and 2D raycasts).</summary>
        public const int DefaultLayers = ~(1 << 2);

        /// <param name="camera">The camera the scene is seen through (passed in: no <c>Camera.main</c>).</param>
        /// <param name="worlds">3D, 2D or both (default).</param>
        /// <param name="layers">Layers to hit. Default: Unity's default raycast layers.</param>
        /// <param name="maxDistance">Maximum distance from the camera.</param>
        /// <param name="component">The collider must be inside a component of this type (e.g. your <c>Unit</c>).</param>
        /// <param name="hitTriggers">Whether 3D triggers count. Null: the project's Physics setting.</param>
        public PhysicsRaycastArea(Camera camera, PhysicsWorld worlds = PhysicsWorld.Both, LayerMask? layers = null,
            float maxDistance = Mathf.Infinity, Type component = null, bool? hitTriggers = null)
        {
            _camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            if ((worlds & PhysicsWorld.Both) == 0) throw new ArgumentException("Choose 3D, 2D or both.", nameof(worlds));
            _worlds = worlds;
            _layers = layers ?? (LayerMask)DefaultLayers;
            _maxDistance = maxDistance;
            if (component != null && !typeof(Component).IsAssignableFrom(component))
                throw new ArgumentException($"{component.Name} is not a Component.", nameof(component));
            _component = component;
            _hitTriggers = hitTriggers;
        }

        /// <summary>Hits on a collider inside a <typeparamref name="T"/>, in 3D, 2D or both.</summary>
        public static PhysicsRaycastArea For<T>(Camera camera, PhysicsWorld worlds = PhysicsWorld.Both, LayerMask? layers = null,
            float maxDistance = Mathf.Infinity) where T : Component =>
            new PhysicsRaycastArea(camera, worlds, layers, maxDistance, typeof(T));

        public bool Accepts(in IntentionPoint point, out object target)
        {
            target = null;
            if (_camera == null || !_camera.isActiveAndEnabled) return false;
            var ray = _camera.ScreenPointToRay(point.PositionPx);

            WorldHit nearest = null;
            if ((_worlds & PhysicsWorld.Physics3D) != 0) nearest = Closer(nearest, Raycast3D(ray));
            if ((_worlds & PhysicsWorld.Physics2D) != 0) nearest = Closer(nearest, Raycast2D(ray));
            target = nearest;
            return nearest != null;
        }

        private static WorldHit Closer(WorldHit a, WorldHit b) => a == null ? b : b == null ? a : b.Distance < a.Distance ? b : a;

        private Component Filter(Component collider) =>
            _component == null ? collider : collider.GetComponentInParent(_component);

        private WorldHit Raycast3D(Ray ray)
        {
#if THREADS_TC_PHYSICS
            // Nearest hit whose collider passes the component filter (a filtered-out collider must not hide one behind it).
            var triggers = _hitTriggers == null ? QueryTriggerInteraction.UseGlobal
                : _hitTriggers.Value ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
            var hits = Physics.RaycastAll(ray, _maxDistance, _layers, triggers);
            WorldHit best = null;
            foreach (var hit in hits)
            {
                if (best != null && hit.distance >= best.Distance) continue;
                var component = Filter(hit.collider);
                if (component == null) continue;
                best = new WorldHit(hit.collider.gameObject, component, hit.point, hit.normal, hit.distance, false);
            }
            return best;
#else
            return null;
#endif
        }

        private WorldHit Raycast2D(Ray ray)
        {
#if THREADS_TC_PHYSICS2D
            WorldHit best = null;
            foreach (var hit in Physics2D.GetRayIntersectionAll(ray, _maxDistance, _layers))
            {
                var component = Filter(hit.collider);
                if (component == null) continue;
                // The ray meets the 2D collider on the collider's z plane: measure the distance there, so 2D
                // and 3D hits compare fairly.
                var z = hit.collider.transform.position.z;
                var distance = Mathf.Abs(ray.direction.z) > 1e-6f ? (z - ray.origin.z) / ray.direction.z : hit.distance;
                if (distance < 0f || distance > _maxDistance) continue;
                if (best != null && distance >= best.Distance) continue;
                best = new WorldHit(hit.collider.gameObject, component, ray.GetPoint(distance), hit.normal, distance, true);
            }
            return best;
#else
            return null;
#endif
        }
    }
}
