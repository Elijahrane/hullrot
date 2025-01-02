using Content.Shared._Hullrot.Logistics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Server._Hullrot.Logistics;

public sealed class LogisticCargoDataSystem : EntitySystem
{

    public override void Initialize()
    {
        SubscribeLocalEvent<LogisticCargoDataComponent, ComponentStartup>(OnCargoTrackerStartup);
        SubscribeLocalEvent<LogisticCargoDataComponent, AnchorStateChangedEvent>(OnCargoTrackerAnchorChange);
    }
}
