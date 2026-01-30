using System.Collections.Generic;

namespace Game.AI.BehaviorTree.Nodes
{
    /// <summary>
    /// Sequence (AND) - runs children until one fails
    /// </summary>
    public class SequenceNode : IBehaviorNode
    {
        private readonly List<IBehaviorNode> _children = new();
        private int _currentIndex;

        public SequenceNode(params IBehaviorNode[] children)
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

                if (status == BehaviorStatus.Failure)
                {
                    _currentIndex = 0;
                    return BehaviorStatus.Failure;
                }

                if (status == BehaviorStatus.Running)
                {
                    return BehaviorStatus.Running;
                }

                _currentIndex++;
            }

            _currentIndex = 0;
            return BehaviorStatus.Success;
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
