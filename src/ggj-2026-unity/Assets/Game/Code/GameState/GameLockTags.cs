using System;

namespace Game.GameState
{
    [Flags]
    public enum GameLockTags
    {
        None = 0,
        PlayerInput = 1 << 0,
        EnemyAI = 1 << 1,
        PlayerMovement = 1 << 2,
        All = PlayerInput | EnemyAI | PlayerMovement
    }
}
