namespace Game.AI.BehaviorTree
{
    public interface IBehaviorNode
    {
        BehaviorStatus Tick();
        void Reset();
    }
}
