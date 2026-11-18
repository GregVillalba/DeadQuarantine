using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public sealed partial class ZombieStalker
{
    struct AcidBall
    {
        public int id;
        public Vector3 origin, velocity, previous;
        public double started;
    }
    sealed class BallVisual
    {
        public GameObject body;
        public Vector3 origin, velocity;
        public double started;
    }
    readonly List<AcidBall> acidBalls = new();
    readonly Dictionary<int, BallVisual> acidVisuals = new();
    readonly List<int> expiredVisuals = new();
    Material acidBallMaterial;
    int nextBallId;

    void FireAcidBall()
    {
        if (!IsServer || !mouth || !ValidTarget(target) || OverlappingBody(mouth.position, acidBallRadius, true) || !ClearShot(mouth.position, aimPoint.Value)) return;
        Vector3 delta = aimPoint.Value - mouth.position;
        if (delta.sqrMagnitude < 1 || delta.magnitude > attackRange) return;
        float flight = delta.magnitude / Mathf.Max(1, acidBallSpeed);
        Vector3 velocity = delta / flight + Vector3.up * (.5f * acidGravity * flight);
        int id = ++nextBallId;
        double now = NetworkManager.ServerTime.Time;
        acidBalls.Add(new AcidBall { id = id, origin = mouth.position, velocity = velocity, previous = mouth.position, started = now });
        LaunchAcidClientRpc(id, mouth.position, velocity, now);
    }
    static Vector3 BallPosition(Vector3 origin, Vector3 velocity, float elapsed, float gravity) =>
        origin + velocity * elapsed + Vector3.down * (.5f * gravity * elapsed * elapsed);
    void UpdateAcidProjectiles()
    {
        double now = NetworkManager.ServerTime.Time;
        for (int i = acidBalls.Count - 1; i >= 0; i--)
        {
            var ball = acidBalls[i];
            float elapsed = (float)(now - ball.started);
            Vector3 next = BallPosition(ball.origin, ball.velocity, elapsed, acidGravity);
            var overlap = OverlappingBody(ball.previous, acidBallRadius);
            bool contact = FirstContact(ball.previous, next, acidBallRadius, out var hit);
            Vector3 contactPoint = overlap ? overlap.ClosestPoint(ball.previous) : contact ? hit.point : next;
            Vector3 normal = overlap ? (ball.previous - contactPoint).normalized : contact ? hit.normal : Vector3.up;
            if (normal.sqrMagnitude < .1f) normal = Vector3.up;
            contact |= overlap;
            if (contact || elapsed > attackRange / Mathf.Max(1, acidBallSpeed))
            {
                if (contact)
                {
                    var player = (overlap ? overlap : hit.collider).GetComponentInParent<PlayerHealth>();
                    if (ValidTarget(player))
                    {
                        player.TakeDamage(acidImpactDamage, source: ball.origin);
                        player.ApplyStalkerAcidServer(acidDuration, acidDamagePerSecond, ball.origin);
                    }
                }
                StopAcidClientRpc(ball.id, contactPoint, normal, contact);
                acidBalls.RemoveAt(i);
            }
            else { ball.previous = next; acidBalls[i] = ball; }
        }
    }
    static Collider OverlappingBody(Vector3 point, float radius, bool ignorePlayers = false)
    {
        Collider player = null;
        foreach (var body in Physics.OverlapSphere(point, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (body.GetComponentInParent<ZombieHealth>()) continue;
            if (body.GetComponentInParent<PlayerHealth>()) { if (!ignorePlayers) player = body; }
            else return body; // A solid obstacle always wins an overlapping player collider.
        }
        return player;
    }
    static bool FirstContact(Vector3 from, Vector3 to, float radius, out RaycastHit first, bool ignorePlayers = false)
    {
        first = default; float closest = float.PositiveInfinity;
        Vector3 step = to - from;
        if (step.sqrMagnitude < .000001f) return false;
        var hits = radius > 0
            ? Physics.SphereCastAll(from, radius, step.normalized, step.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            : Physics.RaycastAll(from, step.normalized, step.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            if (hit.collider.GetComponentInParent<ZombieHealth>() || ignorePlayers && hit.collider.GetComponentInParent<PlayerHealth>()) continue;
            if (hit.distance >= closest) continue;
            closest = hit.distance; first = hit;
        }
        return !float.IsPositiveInfinity(closest);
    }
    [ClientRpc]
    void LaunchAcidClientRpc(int id, Vector3 origin, Vector3 velocity, double started)
    {
        if (!acidBallMaterial)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader) shader = Shader.Find("Sprites/Default");
            acidBallMaterial = new Material(shader) { name = "Acechador ácido (runtime)", color = new Color(.42f, .95f, .04f) };
        }
        var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        body.name = "Bola de ácido del Acechador";
        var collider = body.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
        body.transform.localScale = Vector3.one * acidBallRadius * 2;
        body.transform.position = origin;
        var renderer = body.GetComponent<Renderer>(); renderer.sharedMaterial = acidBallMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
        var trail = body.AddComponent<TrailRenderer>(); trail.sharedMaterial = acidBallMaterial;
        trail.time = .16f; trail.startWidth = acidBallRadius * 1.3f; trail.endWidth = .025f;
        trail.startColor = new Color(.5f, .9f, .02f, .65f); trail.endColor = new Color(.3f, .6f, .01f, 0);
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        acidVisuals[id] = new BallVisual { body = body, origin = origin, velocity = velocity, started = started };
    }
    [ClientRpc]
    void StopAcidClientRpc(int id, Vector3 point, Vector3 normal, bool splash)
    {
        if (acidVisuals.Remove(id, out var visual) && visual.body) Destroy(visual.body);
        if (!splash || !acidMaterial) return;
        var effect = new GameObject("Salpicadura de ácido del Acechador");
        effect.transform.SetPositionAndRotation(point + normal * .03f, Quaternion.LookRotation(normal));
        var particles = effect.AddComponent<ParticleSystem>(); particles.Stop();
        var main = particles.main; main.loop = false; main.duration = .5f; main.startLifetime = new ParticleSystem.MinMaxCurve(.18f, .45f);
        main.startSize = new ParticleSystem.MinMaxCurve(.045f, .15f); main.startSpeed = new ParticleSystem.MinMaxCurve(1, 3);
        main.startColor = new Color(.45f, .9f, .04f, .8f); main.gravityModifier = .6f;
        var emission = particles.emission; emission.rateOverTime = 0; emission.SetBursts(new[] { new ParticleSystem.Burst(0, 18) });
        var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Hemisphere; shape.radius = .15f;
        particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = acidMaterial; particles.Play(); Destroy(effect, 1);
    }
    void UpdateAcidVisuals()
    {
        if (!IsClient || NetworkManager == null) return;
        expiredVisuals.Clear();
        foreach (var pair in acidVisuals)
        {
            var ball = pair.Value; float elapsed = Mathf.Max(0, (float)(NetworkManager.ServerTime.Time - ball.started));
            if (!ball.body || elapsed > attackRange / Mathf.Max(1, acidBallSpeed) + .5f) { expiredVisuals.Add(pair.Key); continue; }
            ball.body.transform.position = BallPosition(ball.origin, ball.velocity, elapsed, acidGravity);
        }
        foreach (var id in expiredVisuals) { if (acidVisuals[id].body) Destroy(acidVisuals[id].body); acidVisuals.Remove(id); }
    }
    void ClearAcidProjectiles()
    {
        acidBalls.Clear();
        foreach (var ball in acidVisuals.Values) if (ball.body) Destroy(ball.body);
        acidVisuals.Clear();
        if (acidBallMaterial) Destroy(acidBallMaterial);
        acidBallMaterial = null;
    }
}
