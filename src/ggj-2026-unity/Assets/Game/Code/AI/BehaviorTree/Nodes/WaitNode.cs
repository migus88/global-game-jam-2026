using UnityEngine;

namespace Game.AI.BehaviorTree.Nodes
{
    /// <summary>
    /// Waits for a specified duration, returns Running until time elapses.
    /// </summary>
    public class WaitNode : IBehaviorNode
    {
        private readonly float _duration;
        private float _elapsedTime;
        private bool _started;

        public WaitNode(float duration)
        {
            _duration = duration;
        }

        public BehaviorStatus Tick()
        {
            if (!_started)
            {
                _started = true;
                _elapsedTime = 0f;
            }

            _elapsedTime += Time.deltaTime;

            if (_elapsedTime >= _duration)
            {
                return BehaviorStatus.Success;
            }

            return BehaviorStatus.Running;
        }

        public void Reset()
        {
            _started = false;
            _elapsedTime = 0f;
        }
    }
}
