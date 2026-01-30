using System;

namespace Game.AI.BehaviorTree.Nodes
{
    public class ActionNode : IBehaviorNode
    {
        private readonly Func<BehaviorStatus> _action;

        public ActionNode(Func<BehaviorStatus> action)
        {
            _action = action;
        }

        public BehaviorStatus Tick()
        {
            return _action();
        }

        public void Reset()
        {
        }
    }
}
