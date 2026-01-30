# Conversation System Setup Guide

This document explains how to set up the conversation system that triggers when an enemy catches the player.

## Overview

When an enemy's vision cone fully detects the player (red bar fills up), the game pauses and enters "conversation mode". The robot asks the player a question and they must select a correct answer. Correct answer = continue playing (robot ignores you). Wrong answer = game over.

---

## Required Assets to Create

### 1. ConversationConfiguration (ScriptableObject)

**Create:** Right-click in Project > Create > Game > Conversation Configuration

Configure:
- **Questions** - Array of questions the robot can ask
  - **Audio Clip** - Robot voice audio
  - **Text** - What the robot says (displayed on screen)
  - **Answers** - Up to 4 answer options
    - **Text** - Answer text shown on button
    - **Is Correct** - Check if this is a valid answer (multiple can be correct, or none)

- **Correct Responses** - Robot's responses when player answers correctly
  - **Audio Clip** - Robot voice audio
  - **Text** - What the robot says

- **Incorrect Responses** - Robot's responses when player answers wrong
  - **Audio Clip** - Robot voice audio  
  - **Text** - What the robot says

- **Timing**
  - **Delay Before Question** - Seconds to wait before showing question (default: 0.5)
  - **Delay After Response** - Seconds to wait after response before resuming/game over (default: 1)

---

## Scene Setup

### 2. GameLifetimeScope

Add your `ConversationConfiguration` asset to the `GameLifetimeScope` component's **Conversation Configuration** field.

### 3. GameStateManager

**Create:** Empty GameObject named `[GameStateManager]`

**Add Component:** `GameStateManager`

This handles pausing/resuming the game via `Time.timeScale`.

### 4. ConversationManager

**Create:** Empty GameObject named `[ConversationManager]`

**Add Component:** `ConversationManager`

**Assign:** Reference to `ConversationUI` (see below)

### 5. ConversationUI (Canvas)

**Create:** UI Canvas with the following hierarchy:

```
ConversationCanvas
├── Container (Panel)
│   ├── DialogueText (TextMeshPro) - Shows question, then response
│   └── AnswersContainer
│       ├── AnswerButton0 (Button + TextMeshPro child)
│       ├── AnswerButton1 (Button + TextMeshPro child)
│       ├── AnswerButton2 (Button + TextMeshPro child)
│       └── AnswerButton3 (Button + TextMeshPro child)
```

**Add Component:** `ConversationUI` to the root Canvas

**Assign:**
- **Container** - The main panel that shows/hides
- **Dialogue Text** - TextMeshPro for robot's dialogue (question and response)
- **Answers Container** - Parent of answer buttons
- **Answer Buttons** - Array of 4 Button components
- **Answer Texts** - Array of 4 TextMeshPro components (children of buttons)

### 6. GameOverUI

**Create:** UI Canvas with game over screen

```
GameOverCanvas
└── Container (Panel)
    └── GameOverTitle (TextMeshPro) - "GAME OVER"
```

**Add Component:** `GameOverUI`

**Assign:**
- **Container** - The panel that shows/hides

---

## Enemy Setup

### 7. Each Enemy Robot

**Add Component:** `EnemyConversationHandler` to the root enemy GameObject

**Create:** Cinemachine Camera as a child of the enemy, pointed at the robot's face

**Assign on EnemyConversationHandler:**
- **Conversation Camera** - The CinemachineCamera component
- **Conversation Camera Priority** - Higher than main camera (default: 20)
- **Vision Cone** - Reference to the VisionCone component (auto-finds if not set)

**Camera Setup Tips:**
- Position the camera to frame the robot's face nicely
- Set the CinemachineCamera's default Priority low (e.g., 0 or 5)
- During conversation, priority is raised to take over the view
- After conversation ends, priority is restored

---

## Player Setup

### 8. Lockable Player Input

**Add Component:** `LockablePlayerInput` to the Player GameObject

**Assign:** 
- **Player Input** - Reference to PlayerInput component (auto-finds if not set)

This automatically disables player input when locked (during conversation) and re-enables when unlocked.

### 9. Lockable Player Movement

**Add Component:** `LockablePlayerMovement` to the Player GameObject

**Assign:**
- **Movement Controller** - Reference to the movement MonoBehaviour (e.g., ThirdPersonController)

This disables player movement when locked (during hiding or conversation).

---

## Event Flow

1. `VisionCone` detects player fully → publishes `PlayerCaughtEvent`
2. `ConversationManager` receives event → publishes `GamePausedEvent`
3. `GameStateManager` receives pause → sets `Time.timeScale = 0`
4. `PausableInputHandler` receives pause → disables input
5. `EnemyConversationHandler` receives `ConversationStartedEvent` → raises camera priority
6. Player selects answer
7. `ConversationManager` evaluates answer → publishes `ConversationEndedEvent`
8. `EnemyConversationHandler` receives end → restores camera, optionally disables vision cone
9. If correct: `GameResumedEvent` → game continues, robot ignores player
10. If incorrect: `GameOverEvent` → game over screen shows, game stays frozen

---

## Events Reference

**Game State Events** (`Game.GameState.Events`):
- `GamePausedEvent` - Pause the game (includes `PauseReason`)
- `GameResumedEvent` - Resume the game
- `GameOverEvent` - Game over (includes reason string)

**Conversation Events** (`Game.Conversation.Events`):
- `PlayerCaughtEvent` - Enemy caught player (enemy transform, player transform)
- `ConversationStartedEvent` - Conversation began (enemy transform)
- `ConversationEndedEvent` - Conversation ended (was correct, enemy transform)

---

## Making Other Systems Lockable

The system uses [MLock](https://github.com/migus88/MLock) for locking functionality during conversations. Systems implement `ILockable<GameLockTags>` to respond to locks.

**Available Lock Tags** (`GameLockTags`):
- `PlayerInput` - Player input handling
- `EnemyAI` - Enemy behavior/AI
- `PlayerMovement` - Player movement
- `All` - All of the above

**Implement ILockable:**
```csharp
using Game.GameState;
using Migs.MLock;

public class MyLockableSystem : MonoBehaviour, ILockable<GameLockTags>
{
    private GameLockService _lockService;
    private bool _isLocked;

    public GameLockTags LockTags => GameLockTags.PlayerInput; // Which tags affect this

    private void Start()
    {
        // Resolve _lockService from LifetimeScope...
        _lockService?.Subscribe(this);
    }

    private void OnDestroy()
    {
        _lockService?.Unsubscribe(this);
    }

    public void HandleLocking()
    {
        _isLocked = true;
        // Disable your system
    }

    public void HandleUnlocking()
    {
        _isLocked = false;
        // Re-enable your system
    }

    private void Update()
    {
        if (_isLocked) return;
        // Your normal update logic
    }
}
```

**Locking from code:**
```csharp
// Lock specific tags
ILock<GameLockTags> myLock = _lockService.Lock(GameLockTags.PlayerInput);

// Lock everything
ILock<GameLockTags> myLock = _lockService.Lock(GameLockTags.All);

// Unlock by disposing
myLock.Dispose();
```

---

## Quick Checklist

- [ ] Create `ConversationConfiguration` asset with questions/answers/responses
- [ ] Assign `ConversationConfiguration` to `GameLifetimeScope`
- [ ] Add `GameStateManager` to scene
- [ ] Add `ConversationManager` to scene
- [ ] Create `ConversationUI` canvas and assign references
- [ ] Create `GameOverUI` canvas and assign references
- [ ] Add `EnemyConversationHandler` to each enemy
- [ ] Create CinemachineCamera for each enemy's face
- [ ] Add `LockablePlayerInput` to player
- [ ] Add `LockablePlayerMovement` to player (assign ThirdPersonController)
