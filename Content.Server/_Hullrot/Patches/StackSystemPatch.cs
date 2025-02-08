using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Content.Shared.Stacks;
using Content.Server.Stack;
using Robust.Shared.Prototypes;
using System.Collections;

namespace Content.Server.Stack;


 public sealed partial class StackSystem
{
    private Dictionary<string, bool> isStackablePrototype = new();
    private Dictionary<string, int> StackMaxAmount = new();

    private int getMaxStackCount(string prototypeId)
    {
        if (!isStackablePrototype.ContainsKey(prototypeId))
            buildStackable(prototypeId);
        return StackMaxAmount[prototypeId];
    }

    private void buildStackable(string prototypeId)
    {
        var tempEntity = EntityManager.CreateEntityUninitialized(prototypeId);
        if (!TryComp<StackComponent>(tempEntity, out var stackComponent))
        {
            isStackablePrototype.Add(prototypeId, false);
            StackMaxAmount.Add(prototypeId, 1);
        }
        else
        {
            isStackablePrototype.Add(prototypeId, true);
            StackMaxAmount.Add(prototypeId, GetMaxCount(tempEntity));
        }
        EntityManager.DeleteEntity(tempEntity);
    }
    public bool isStackable(string prototypeId)
    {
        if (!isStackablePrototype.ContainsKey(prototypeId))
            buildStackable(prototypeId);
        return isStackablePrototype[prototypeId];
    }



    public List<EntityUid> mergeStackEntities(List<EntityUid> targets)
    {
        var returnList = new List<EntityUid>(targets);
        var compDictionary = new Dictionary<EntityUid, StackComponent>();
        var nonFullStacks = new List<EntityUid>();
        foreach (var stack in targets)
        {
            var comp = Comp<StackComponent>(stack);
            compDictionary.Add(stack, comp);
            if (GetAvailableSpace(comp) == 0)
                continue;
            if (nonFullStacks.Count == 0)
            {
                nonFullStacks.Add(stack);
                continue;
            }

            // this can only fail if the first stack is full in our context
            if (!TryMergeStacks(stack, nonFullStacks[0], out var moved))
            {
                nonFullStacks.RemoveAt(0);
            }

            if (comp.Count == 0)
            {
                returnList.Remove(stack);
                continue;
            }

            nonFullStacks.Add(stack);
        }
        returnList.Sort((first, second) =>
        {
            var fComp = compDictionary[first];
            var sComp = compDictionary[second];
            if (fComp.Count > sComp.Count)
                return 1;
            if (fComp.Count < sComp.Count)
                return -1;
            return 0;
        });

        return returnList;
    }
}
