using System;
using System.Collections.Generic;
using UnityEngine;

// The host and clients simulate the same short spray from their animated head pose.
internal sealed class ZombieBossAcidSpray
{
    private struct Drop
    {
        public Vector3 position, velocity;
        public float age, life, size;
        public Color32 color;
        public bool splash;
    }

    private const int Capacity = 960;
    private const float BurstInterval = .016f;
    private readonly List<Drop> drops = new(Capacity);
    private readonly ParticleSystem.Particle[] particles = new ParticleSystem.Particle[Capacity];
    private readonly RaycastHit[] contacts = new RaycastHit[24];
    private readonly Collider[] mouthContacts = new Collider[16];
    private ParticleSystem effect;
    private float timeToBurst;
    private uint seed = 1;

    internal void Step(Transform mouth, Quaternion heading, float range, float spread, Material material,
        bool show, Action<PlayerHealth, Vector3> hitPlayer,
        float speedMultiplier = 1f, float gravityStrength = 8f, float visualScale = 1f)
    {
        float dt = Time.deltaTime;
        timeToBurst -= dt;
        while (timeToBurst <= 0)
        {
            timeToBurst += BurstInterval;
            if (!MouthBlocked(mouth.position)) EmitBurst(mouth.position, heading, range, spread, speedMultiplier, visualScale);
        }

        Vector3 gravity = Vector3.down * Mathf.Clamp(gravityStrength, 0, 20);
        for (int i = drops.Count - 1; i >= 0; i--)
        {
            Drop drop = drops[i];
            drop.age += dt;
            if (drop.age >= drop.life)
            {
                Remove(i);
                continue;
            }

            Vector3 next = drop.position + drop.velocity * dt + gravity * (.5f * dt * dt);
            if (!drop.splash && Trace(drop.position, next, drop.size * .45f, out RaycastHit hit))
            {
                PlayerHealth player = hit.collider.GetComponentInParent<PlayerHealth>();
                if (player != null) hitPlayer?.Invoke(player, mouth.position);
                Remove(i);
                if (show && i % 3 == 0)
                {
                    uint burstSeed = seed;
                    Splash(hit.point, hit.normal);
                    seed = burstSeed;
                }
                continue;
            }

            drop.position = next;
            drop.velocity += gravity * dt;
            drops[i] = drop;
        }

        if (show && material) Draw(mouth, material);
    }

    private void EmitBurst(Vector3 mouth, Quaternion heading, float range, float spread, float speedMultiplier, float visualScale)
    {
        for (int i = 0; i < 9 && drops.Count < Capacity; i++)
        {
            float angle = (i * 2.39996f) + Random01() * .45f;
            float radius = i < 2 ? .04f : Mathf.Sqrt((i - 1f) / 7f);
            float tangent = Mathf.Tan(spread * Mathf.Deg2Rad) * radius;
            Vector3 local = new(Mathf.Cos(angle) * tangent, Mathf.Sin(angle) * tangent * .55f, 1);
            float speed = Mathf.Lerp(16f, 22f, Random01()) * Mathf.Clamp(speedMultiplier, .1f, 3);
            drops.Add(new Drop
            {
                position = mouth, velocity = heading * local.normalized * speed,
                life = range / speed, size = (i < 2 ? .32f : Mathf.Lerp(.055f, .19f, Random01())) * Mathf.Clamp(visualScale, .1f, 2),
                color = new Color32((byte)Mathf.Lerp(75, 164, Random01()), (byte)Mathf.Lerp(173, 250, Random01()), 26, 255)
            });
        }
    }

    private void Splash(Vector3 point, Vector3 normal)
    {
        for (int k = 0; k < 5 && drops.Count < Capacity; k++)
        {
            var scatter = new Vector3(Random01() - .5f, Random01() * .5f, Random01() - .5f);
            drops.Add(new Drop
            {
                position = point + normal * .045f, velocity = (normal + scatter * 1.6f) * Mathf.Lerp(1, 3, Random01()),
                life = .19f, size = Mathf.Lerp(.045f, .115f, Random01()),
                color = new Color32(117, 215, 23, 250), splash = true
            });
        }
    }

    private bool MouthBlocked(Vector3 position)
    {
        int count = Physics.OverlapSphereNonAlloc(position, .07f, mouthContacts,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider shape = mouthContacts[i];
            if (!shape.GetComponentInParent<ZombieHealth>() && !shape.GetComponentInParent<PlayerHealth>())
                return true;
        }
        return false;
    }

    private bool Trace(Vector3 from, Vector3 to, float radius, out RaycastHit closest)
    {
        Vector3 direction = to - from;
        float length = direction.magnitude;
        closest = default;
        if (length < .0001f) return false;
        direction /= length;
        int count = Physics.SphereCastNonAlloc(from, radius, direction, contacts, length,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        var hits = count < contacts.Length ? contacts : Physics.SphereCastAll(from, radius, direction, length,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float distance = float.PositiveInfinity;
        for (int i = 0; i < (hits == contacts ? count : hits.Length); i++)
        {
            RaycastHit hit = hits[i];
            if (hit.distance >= distance || hit.collider.GetComponentInParent<ZombieHealth>()) continue;
            distance = hit.distance;
            closest = hit;
        }
        return closest.collider != null;
    }

    private void Draw(Transform mouth, Material material)
    {
        if (!effect)
        {
            var visual = new GameObject("Zombie boss acid spray");
            visual.transform.SetParent(mouth, false);
            effect = visual.AddComponent<ParticleSystem>();
            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = effect.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Capacity;
            main.simulationSpeed = 0; // Positions and collisions are simulated once by Step.
            main.startSpeed = 0;
            var emission = effect.emission;
            emission.enabled = false;
            var renderer = effect.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV
            });
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.cameraVelocityScale = 0;
            renderer.velocityScale = 0;
            renderer.lengthScale = 1;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            effect.Play();
        }
        for (int i = 0; i < drops.Count; i++)
        {
            Drop drop = drops[i];
            particles[i] = new ParticleSystem.Particle
            {
                position = drop.position, velocity = drop.velocity, startColor = drop.color, startSize = drop.size,
                startLifetime = 1f, remainingLifetime = 1f
            };
        }
        effect.SetParticles(particles, drops.Count);
    }

    internal void Clear()
    {
        if (drops.Count == 0 && timeToBurst == 0) return;
        drops.Clear();
        timeToBurst = 0;
        if (effect) effect.Clear();
    }

    internal void Dispose() { if (effect) UnityEngine.Object.Destroy(effect.gameObject); }

    private float Random01()
    {
        seed = unchecked(seed * 1664525u + 1013904223u);
        return (seed >> 8) / 16777216f;
    }

    private void Remove(int index)
    {
        drops[index] = drops[drops.Count - 1];
        drops.RemoveAt(drops.Count - 1);
    }
}