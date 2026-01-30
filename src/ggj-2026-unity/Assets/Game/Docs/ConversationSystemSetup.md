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
│   ├── QuestionText (TextMeshPro)
│   ├── AnswersContainer
│   │   ├── AnswerButton0 (Button + TextMeshPro child)
│   │   ├── AnswerButton1 (Button + TextMeshPro child)
│   │   ├── AnswerButton2 (Button + TextMeshPro child)
│   │   └── AnswerButton3 (Button + TextMeshPro child)
│   └── ResponseContainer
│       └── ResponseText (TextMeshPro)
```

**Add Component:** `ConversationUI` to the root Canvas

**Assign:**
- **Container** - The main panel that shows/hides
- **Question Text** - TextMeshPro for robot's question
- **Answers Container** - Parent of answer buttons
- **Answer Buttons** - Array of 4 Button components
- **Answer Texts** - Array of 4 TextMeshPro components (children of buttons)
- **Response Container** - Panel for robot's response
- **Response Text** - TextMeshPro for robot's response

### 6. GameOverUI

**Create:** UI Canvas with game over screen

```
GameOverCanvas
└── Container (Panel)
    ├── GameOverTitle (TextMeshPro) - "GAME OVER"
    └── ReasonText (TextMeshPro) - Shows why (e.g., "You were caught!")
```

**Add Component:** `GameOverUI`

**Assign:**
- **Container** - The panel that shows/hides
- **Reason Text** - TextMeshPro to display the reason

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

### 8. Player Input Handler

**Add Component:** `PausableInputHandler` to the Player GameObject

**Assign:** 
- **Player Input** - Reference to PlayerInput component (auto-finds if not set)

This automatically disables player input when game is paused and re-enables on resume.

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

## Making Other Systems Pause-Aware

To make any system respond to pause/resume:

```csharp
using Game.Events;
using Game.GameState.Events;

public class MyPausableSystem : MonoBehaviour
{
    private EventAggregator _eventAggregator;
    private bool _isPaused;

    private void Start()
    {
        // Resolve _eventAggregator from LifetimeScope...
        
        _eventAggregator?.Subscribe<GamePausedEvent>(OnPaused);
        _eventAggregator?.Subscribe<GameResumedEvent>(OnResumed);
    }

    private void OnPaused(GamePausedEvent evt)
    {
        _isPaused = true;
        // Disable your system's update logic
    }

    private void OnResumed(GameResumedEvent evt)
    {
        _isPaused = false;
        // Re-enable your system's update logic
    }

    private void Update()
    {
        if (_isPaused) return;
        // Your normal update logic
    }

    private void OnDestroy()
    {
        _eventAggregator?.Unsubscribe<GamePausedEvent>(OnPaused);
        _eventAggregator?.Unsubscribe<GameResumedEvent>(OnResumed);
    }
}
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
- [ ] Add `PausableInputHandler` to player
