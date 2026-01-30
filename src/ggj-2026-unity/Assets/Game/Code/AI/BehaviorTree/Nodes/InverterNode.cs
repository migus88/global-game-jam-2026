namespace Game.AI.BehaviorTree.Nodes
{
    /// <summary>
    /// Inverts the result of its child: Success becomes Failure, Failure becomes Success.
    /// Running stays Running.
    /// </summary>
    public class InverterNode : IBehaviorNode
    {
        private readonly IBehaviorNode _child;

        public InverterNode(IBehaviorNode child)
        {
            _child = child;
        }

        public BehaviorStatus Tick()
        {
            var status = _child.Tick();

            return status switch
            {
                BehaviorStatus.Success => BehaviorStatus.Failure,
                BehaviorStatus.Failure => BehaviorStatus.Success,
                _ => status
            };
        }

        public void Reset()
        {
            _child.Reset();
        }
    }
}
