using System;

namespace Game.AI.BehaviorTree.Nodes
{
    public class ConditionNode : IBehaviorNode
    {
        private readonly Func<bool> _condition;

        public ConditionNode(Func<bool> condition)
        {
            _condition = condition;
        }

        public BehaviorStatus Tick()
        {
            return _condition() ? BehaviorStatus.Success : BehaviorStatus.Failure;
        }

        public void Reset()
        {
        }
    }
}
