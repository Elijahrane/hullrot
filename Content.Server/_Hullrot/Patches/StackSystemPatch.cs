using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Content.Shared.Stacks;
using Content.Server.Stack;
using Robust.Shared.Prototypes;

namespace Content.Server.Stack;


 public sealed partial class StackSystem
{
    private FrozenDictionary<string, bool> isStackablePrototype = new();
    private FrozenDictionary<string, int> StackMaxAmount = new();


    private void HullrotInitialize()
    {
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReload);
    }

    private void OnPrototypesReload(PrototypesReloadedEventArgs args)
    {

    }
    private void checkStackable(string prototypeId)
    {

    }
    public bool isStackable(string prototypeId)
    {
        if (!isStackablePrototype.ContainsKey(prototypeId))
        {
            var tempEntity = EntityManager.CreateEntityUninitialized(prototypeId);
            if (!TryComp<StackComponent>(tempEntity, out var stackComponent))
                isStackablePrototype.Add(prototypeId, false);
            else
            {
                isStackablePrototype.Add(prototypeId, true);
                StackMaxAmount.Add(prototypeId, GetMaxCount(tempEntity));
            }
        }

        return isStackablePrototype[prototypeId];
    }
}
