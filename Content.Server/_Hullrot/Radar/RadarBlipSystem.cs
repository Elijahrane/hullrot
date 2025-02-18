using System.Numerics;
using Content.Server.Shuttles.Systems;
using Content.Shared._Hullrot.Radar;
using Content.Shared.Shuttles.Components;

namespace Content.Server._Hullrot.Radar;

/// <summary>
/// I debated making <see cref="RadarConsoleSystem"/> partial
/// but ended up doing this instead to mnimize conflicts. This system
/// handles radar blips -- which, due to both the limitations of PVS range
/// and against giving the client too much info must be server side.
/// </summary>
public sealed partial class RadarBlipSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RequestBlipsEvent>(OnBlipsRequested);
    }

    private void OnBlipsRequested(RequestBlipsEvent ev, EntitySessionEventArgs args)
    {
        if (!TryGetEntity(ev.Radar, out var radarUid))
            return;

        if (!TryComp<RadarConsoleComponent>(radarUid, out var radar))
            return;

        var blips = AssembleBlipsReport((EntityUid)radarUid, radar);

        var giveEv = new GiveBlipsEvent(blips);
        RaiseNetworkEvent(giveEv, args.SenderSession);
    }

    private List<(Vector2, float, Color)> AssembleBlipsReport(EntityUid uid, RadarConsoleComponent? component = null)
    {
        var blips = new List<(Vector2, float, Color)>();

        if (Resolve(uid, ref component))
        {
            var blipQuery = EntityQueryEnumerator<RadarBlipComponent, TransformComponent>();

            // add blips, except
            while (blipQuery.MoveNext(out var blipUid, out var blip, out var _))
            {
                // case 1: component disabled
                if (!blip.Enabled)
                    continue;

                // case 2: blip out of radar's max range
                var distance = (_xform.GetWorldPosition(blipUid) - _xform.GetWorldPosition(uid)).Length();
                if (distance > component.MaxRange)
                    continue;

                // case 3: On grid but will only show up off grid
                if (blip.RequireNoGrid && _xform.GetGrid(blipUid) != null)
                    continue;

                blips.Add((_xform.GetWorldPosition(blipUid), blip.Scale, blip.Color));
            }
        }

        return blips;
    }
}
