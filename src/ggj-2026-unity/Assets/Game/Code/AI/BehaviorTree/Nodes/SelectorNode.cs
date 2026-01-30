using System.Collections.Generic;

namespace Game.AI.BehaviorTree.Nodes
{
    /// <summary>
    /// Selector (OR) - runs children until one succeeds
    /// </summary>
    public class SelectorNode : IBehaviorNode
    {
        private readonly List<IBehaviorNode> _children = new();
        private int _currentIndex;

        public SelectorNode(params IBehaviorNode[] children)
        {
            _children.AddRange(children);
        }

        public void AddChild(IBehaviorNode child)
        {
            _children.Add(child);
        }

        public BehaviorStatus Tick()
        {
            while (_currentIndex < _children.Count)
            {
                var status = _children[_currentIndex].Tick();

                if (status == BehaviorStatus.Success)
                {
                    _currentIndex = 0;
                    return BehaviorStatus.Success;
                }

                if (status == BehaviorStatus.Running)
                {
                    return BehaviorStatus.Running;
                }

                _currentIndex++;
            }

            _currentIndex = 0;
            return BehaviorStatus.Failure;
        }

        public void Reset()
        {
            _currentIndex = 0;
            foreach (var child in _children)
            {
                child.Reset();
            }
        }
    }
}
