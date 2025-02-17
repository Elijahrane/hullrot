using System.Linq;
using System.Numerics;
using Content.Shared.Interaction;
using Content.Server.Shuttles.Components;
using Content.Shared.Physics;
using Content.Shared.Projectiles;
using Robust.Server.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Random;

namespace Content.Server._Hullrot.HeatSeeking;

/// <summary>
/// This handles...
/// </summary>
public sealed class HeatSeekingSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly RotateToFaceSystem _rotate = default!;
    [Dependency] private readonly PhysicsSystem _physics = default!;
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HeatSeekingComponent, TransformComponent>(); // get all heat seeking missiles
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (comp.Speed < comp.InitialSpeed) { comp.Speed = comp.InitialSpeed; } // start at initial speed
            if (comp.Speed < comp.TopSpeed) { comp.Speed += comp.Acceleration * frameTime; } else { comp.Speed = comp.TopSpeed; } // accelerate to top speed once target is locked
            _physics.SetLinearVelocity(uid, _transform.GetWorldRotation(xform).ToWorldVec() * comp.Speed); // move missile forward at current speed
            if (comp.TargetEntity.HasValue) // if the missile has a target, run its guidance algorithm
            {
                if ((comp.GuidanceAlgorithm & GuidanceType.PredictiveGuidance) != 0) { PredictiveGuidance(uid, comp, xform, frameTime); }
                else if ((comp.GuidanceAlgorithm & GuidanceType.PurePursuit) != 0 ){ PurePursuit(uid, comp, xform, frameTime); }
                else { PredictiveGuidance(uid, comp, xform, frameTime); } // if yaml is invalid, default to Predictive Guidance
            }
            else
            {
                GetNewTarget(uid, comp, xform);
            }
        }
    }

    public void GetNewTarget(EntityUid uid, HeatSeekingComponent component, TransformComponent transform) // Get the closest valid target
    {
        var closestDistance = float.MaxValue;
        EntityUid? closestGrid = null;
        var shipQuery = EntityQueryEnumerator<ShuttleConsoleComponent, TransformComponent>(); // get all shuttle consoles
        while (shipQuery.MoveNext(out var shipUid, out var shipComp, out var shipXform)) // go through each grid with a shuttle console to find the closest valid target
        {
            var angle = (
                _transform.ToMapCoordinates(shipXform.Coordinates).Position -
                _transform.ToMapCoordinates(transform.Coordinates).Position
            ).ToWorldAngle(); // current angle towards target
            var distance = Vector2.Distance(
                _transform.ToMapCoordinates(transform.Coordinates).Position,
                _transform.ToMapCoordinates(shipXform.Coordinates).Position
            ); // current distance from target

            if (angle > _transform.GetWorldRotation(transform) + component.FOV * Math.PI / 180f
            || angle < _transform.GetWorldRotation(transform) - component.FOV * Math.PI / 180f) // if target is out of FOV, skip it.
            {
                continue;
            }
            if (distance > component.SeekRange) // if target is out of range, skip it.
            {
                continue;
            }

            if (TryComp<ProjectileComponent>(uid, out var projectile) && TryComp<TransformComponent>(projectile.Shooter, out var shooterTransform)) // get the shooter of the missile
            {
                var shooterGridUid = shooterTransform.GridUid;
                if (TryComp<TransformComponent>(shipXform.GridUid, out var hitTransform))
                {
                    if (shooterGridUid == hitTransform.GridUid) // if target is the shooter of the missile, skip it.
                    {
                        continue;
                    }
                }
            }
            if (closestDistance > distance) // if this target is the closest target checked so far, save it.
            {
                closestDistance = distance;
                closestGrid = shipXform.GridUid;
            }
        }
        if (closestGrid.HasValue) // after checking all valid targets, pick the closest one.
        {
            component.TargetEntity = closestGrid;
        }
    }
    public void PredictiveGuidance(EntityUid uid, HeatSeekingComponent comp, TransformComponent xform, float frameTime) // Predictive Guidance, predicts targets position at impact time.
    {
        if (comp.TargetEntity.HasValue)
        {
            float oldDistance = comp.oldDistance;
            Vector2 oldPosition = comp.oldPosition;
            var EntXform = Transform(comp.TargetEntity.Value); // get target transform
            //var originalAngle = _transform.GetWorldRotation(xform); // get current angle of missile
            var distance = Vector2.Distance(
                _transform.ToMapCoordinates(xform.Coordinates).Position,
                _transform.ToMapCoordinates(EntXform.Coordinates).Position
            ); // current distance from target

            var targetVelocity = _transform.ToMapCoordinates(EntXform.Coordinates).Position - oldPosition; // get target velocity
            float timeToImpact = distance / (oldDistance - distance); // time it will take for the missile to reach the target
            if (timeToImpact < 0.1) { timeToImpact = 0.1f; } // prevent negative time to impact, that messes up guidance
            var predictedPosition = _transform.ToMapCoordinates(EntXform.Coordinates).Position + (targetVelocity * timeToImpact); // predict target position at impact time

            Angle targetAngle = (predictedPosition - _transform.ToMapCoordinates(xform.Coordinates).Position).ToWorldAngle(); // the angle the missile will try to face

            _rotate.TryRotateTo(uid, targetAngle, frameTime, comp.WeaponArc, comp.RotationSpeed?.Theta ?? double.MaxValue, xform); // rotate towards target angle

            comp.oldPosition = _transform.ToMapCoordinates(EntXform.Coordinates).Position;
            comp.oldDistance = distance;
        }
    }

    public void PurePursuit(EntityUid uid, HeatSeekingComponent comp, TransformComponent xform, float frameTime) // Pure Pursuit, points directly at target.
    {
        if (comp.TargetEntity.HasValue)
        {
            var EntXform = Transform(comp.TargetEntity.Value); // get target transform
            var originalAngle = _transform.GetWorldRotation(xform); // get current angle of missile

            var angle = (
                _transform.ToMapCoordinates(EntXform.Coordinates).Position -
                _transform.ToMapCoordinates(xform.Coordinates).Position
            ).ToWorldAngle(); // current angle towards target

            _rotate.TryRotateTo(uid, angle, frameTime, comp.WeaponArc, comp.RotationSpeed?.Theta ?? double.MaxValue, xform); // rotate towards target angle
        }
    }
}
