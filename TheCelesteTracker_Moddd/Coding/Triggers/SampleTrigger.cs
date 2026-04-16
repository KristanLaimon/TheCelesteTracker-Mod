using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.TheCelesteTracker_Mod.Coding.Triggers
{
    [CustomEntity("TheCelesteTracker_Mod/SampleTrigger")]
    public class SampleTrigger : Trigger
    {
        public SampleTrigger(EntityData data, Vector2 offset) : base(data, offset)
        {
            // TODO: read properties from data
        }
    }
}