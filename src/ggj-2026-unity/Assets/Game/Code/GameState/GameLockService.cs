using System;
using Migs.MLock;
using Migs.MLock.Interfaces;

namespace Game.GameState
{
    public class GameLockService : IDisposable
    {
        public static GameLockService Instance { get; private set; }

        private readonly ILockService<GameLockTags> _lockService;

        public GameLockService()
        {
            _lockService = new BaseLockService<GameLockTags>();
            Instance = this;
        }

        public void Subscribe(ILockable<GameLockTags> lockable)
        {
            _lockService.Subscribe(lockable);
        }

        public void Unsubscribe(ILockable<GameLockTags> lockable)
        {
            _lockService.Unsubscribe(lockable);
        }

        public ILock<GameLockTags> Lock(GameLockTags tags)
        {
            return _lockService.Lock(tags);
        }

        public ILock<GameLockTags> LockAll()
        {
            return _lockService.LockAll();
        }

        public ILock<GameLockTags> LockAllExcept(GameLockTags tags)
        {
            return _lockService.LockAllExcept(tags);
        }

        public bool IsLocked(ILockable<GameLockTags> lockable)
        {
            return _lockService.IsLocked(lockable);
        }

        public void Dispose()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
