using UnityEngine;
#if UNITY_6000_0_OR_NEWER
using PhysMaterial = UnityEngine.PhysicsMaterial;
using PhysMaterialCombine = UnityEngine.PhysicsMaterialCombine;
#else
using PhysMaterial = UnityEngine.PhysicMaterial;
using PhysMaterialCombine = UnityEngine.PhysicMaterialCombine;
#endif

namespace FRC6874
{
    /// <summary>
    /// Wraps the physics APIs Unity 6 renamed (velocity → linearVelocity, drag → linearDamping,
    /// PhysicMaterial → PhysicsMaterial) so the project compiles warning-free on 2022.3 and 6.x.
    /// Always go through these helpers instead of touching those members directly.
    /// </summary>
    public static class PhysicsCompat
    {
        public static Vector3 GetVelocity(this Rigidbody body)
        {
#if UNITY_6000_0_OR_NEWER
            return body.linearVelocity;
#else
            return body.velocity;
#endif
        }

        public static void SetVelocity(this Rigidbody body, Vector3 velocity)
        {
#if UNITY_6000_0_OR_NEWER
            body.linearVelocity = velocity;
#else
            body.velocity = velocity;
#endif
        }

        public static void SetDamping(this Rigidbody body, float linear, float angular)
        {
#if UNITY_6000_0_OR_NEWER
            body.linearDamping = linear;
            body.angularDamping = angular;
#else
            body.drag = linear;
            body.angularDrag = angular;
#endif
        }

        /// <summary>Assigns a (cached) physics material with the given surface properties.</summary>
        public static void SetSurface(Collider collider, float bounciness, float friction, bool maxBounce = false)
        {
            if (collider == null) return;
            collider.sharedMaterial = GetMaterial(bounciness, friction, maxBounce);
        }

        static readonly System.Collections.Generic.Dictionary<string, PhysMaterial> cache =
            new System.Collections.Generic.Dictionary<string, PhysMaterial>();

        static PhysMaterial GetMaterial(float bounciness, float friction, bool maxBounce)
        {
            string key = $"{bounciness:F2}|{friction:F2}|{maxBounce}";
            if (cache.TryGetValue(key, out PhysMaterial cached) && cached != null) return cached;
            var material = new PhysMaterial(key)
            {
                bounciness = bounciness,
                dynamicFriction = friction,
                staticFriction = friction,
                bounceCombine = maxBounce ? PhysMaterialCombine.Maximum : PhysMaterialCombine.Average,
                frictionCombine = PhysMaterialCombine.Average,
            };
            cache[key] = material;
            return material;
        }
    }
}
