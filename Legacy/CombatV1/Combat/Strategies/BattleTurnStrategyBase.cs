using System.Collections;
using UnityEngine;

namespace HalloweenJam.Combat.Strategies
{
    /// <summary>
    /// Base class for turn-resolution strategies so designers can swap behaviours via the inspector.
    /// </summary>
    public abstract class BattleTurnStrategyBase : ScriptableObject
    {
        public abstract IEnumerator ExecuteTurn(BattleTurnContext context);
    }
}

