using System.Numerics;
using Content.Client._Hullrot.Text;
using Content.Client.Parallax;
using Content.Shared._Hullrot.Worldgen;
using Content.Shared._Hullrot.Worldgen.Prototypes;
using Robust.Client.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._Hullrot.WorldGen;

/// <summary>
/// Client-sided entity system that changes music and parallax based on location within a map, if the map is set up for that.
/// </summary>
public sealed partial class WorldZoneAestheticsSystem : EntitySystem
{
    /// <summary>
    /// The current map's zone layout, if any.
    /// </summary>
    private WorldZoneAestheticsPrototype[,]? _curZoneMap;

    /// <summary>
    /// Current zone aesthetics we're in
    /// </summary>
    public WorldZoneAestheticsPrototype CurAesth { get; private set; } = default!;

    /// <summary>
    /// Whether we sent a request to the server and haven't received a response yet
    /// </summary>
    private bool _awaitingUpdate = false;

    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly ParallaxSystem _parallaxSystem = default!;
    [Dependency] private readonly CinematicTextSystem _cineText = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_curZoneMap == null)
            return;

        var playerEnt = _playerManager.LocalEntity;

        if (playerEnt == null || !TryComp<TransformComponent>(playerEnt, out var playerXform))
            return;

        if (_curZoneMap == null)
            return;

        if (!ChunkToArrayCoords(_curZoneMap, WorldToChunkCoords(_xform.GetWorldPosition((EntityUid)playerEnt)), out var arrayCoords))
            return;

        var tileAesth = _curZoneMap[arrayCoords.X, arrayCoords.Y];
        if (tileAesth != CurAesth)
        {
            CurAesth = tileAesth;
            _parallaxSystem.SetParallaxOverride(tileAesth.Parallax);

            if (tileAesth.Name != null)
                _cineText.DrawText(Loc.GetString(tileAesth.Name));
        }
    }

    public override void Initialize()
    {
        base.Initialize();
        CurAesth = _prototypeManager.Index<WorldZoneAestheticsPrototype>("None");
        SubscribeNetworkEvent<GiveMapZoneLayoutEvent>(OnLayoutReceived);
    }

    private void OnLayoutReceived(GiveMapZoneLayoutEvent args, EntitySessionEventArgs msg)
    {
        if (args.Layout != null)
        {
            _curZoneMap = new WorldZoneAestheticsPrototype[args.X, args.Y];

            foreach (var coord in args.Layout)
            {
                _curZoneMap[coord.Item2, coord.Item3] = coord.Item1;
            }

        }
        else
        {
            _curZoneMap = null;
        }
    }

    private Vector2i WorldToChunkCoords(Vector2 input)
    {
        // Same as the const in WorldGen.cs
        var coords = input * new Vector2(1.0f / HullrotWorldGen.ChunkSize, 1.0f / HullrotWorldGen.ChunkSize);
        return new Vector2i((int)Math.Floor(coords.X), (int)Math.Floor(coords.Y));
    }
    private bool ChunkToArrayCoords(WorldZoneAestheticsPrototype[,] array, Vector2i coords, out Vector2i arrayCoords)
    {
        arrayCoords = Vector2i.Zero;

        // These map to 0, 0 in chunk coords
        var baseIndX = array.GetLength(0) / 2;
        var baseIndY = array.GetLength(1) / 2 - 1;

        var indX = baseIndX + coords.X;
        var indY = baseIndY - coords.Y;

        // Check if we're out of bounds
        if (indX < 0 || indX > array.GetLength(0) - 1
        || indY < 0 || indY > array.GetLength(1) - 1)
            return false;

        arrayCoords = new Vector2i(indX, indY);
        return true;
    }

    public WorldZoneAestheticsPrototype[,]? GetCurrentZoneMap()
    {
        return _curZoneMap;
    }
}
