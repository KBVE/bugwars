# VContainer DI Setup Guide

## Overview

This project now uses **VContainer** for dependency injection to manage all core managers and UI components. This provides better lifecycle management, testability, and reduces the need for manual singleton patterns.

## Quick Setup

### 1. Add GameLifetimeScope to Your Scene

1. **Drag the prefab** `Assets/Scripts/Core/GameLifetimeScope.prefab` into your scene
2. **Select the GameLifetimeScope GameObject** in the hierarchy
3. **Verify the references** in the Inspector:
   - **Main Menu Visual Tree**: Should reference `Assets/BugWars/UI/MainMenu/main_menu.uxml`
   - **Panel Settings**: Should reference `Assets/BugWars/UI/MainMenu/MainMenuPanelSettings.asset`

That's it! VContainer will now automatically manage:
- EventManager
- InputManager
- GameManager
- MainMenuManager

## How It Works

### GameLifetimeScope

`GameLifetimeScope` is the root DI container that:

1. **Finds or creates** all core managers (EventManager, InputManager, GameManager)
2. **Creates MainMenuManager** with proper UIDocument configuration
3. **Registers everything** as singletons in the DI container
4. **Ensures DontDestroyOnLoad** for persistence across scene changes

### Execution Flow

```
Scene Loads
    ↓
GameLifetimeScope.Awake() called
    ↓
VContainer.Configure() creates managers
    ↓
Each manager's Awake() sets its static Instance
    ↓
Your code can now use Manager.Instance
```

## Main Menu Escape Key Flow

When you press **Escape**, here's what happens:

1. **InputManager** (line 73) detects the Escape key press
2. **EventManager.TriggerEscapePressed()** fires the event
3. **GameManager.OnEscapePressed()** handler is called
4. **GameManager.ToggleMainMenu()** calls MainMenuManager
5. **MainMenuManager.ToggleMenu()** shows/hides the UI

### Why This Fixes the Menu Issue

Previously, **MainMenuManager was never instantiated** because:
- It didn't auto-create like other managers
- There was no GameObject with MainMenuManager in the scene
- No UIDocument was configured

Now, **VContainer handles everything**:
- Creates MainMenuManager automatically
- Adds and configures UIDocument component
- Assigns the main_menu.uxml visual tree
- Makes it persistent with DontDestroyOnLoad

## Testing

To verify everything works:

1. **Open Unity** and let it import VContainer
2. **Open your scene** (e.g., SampleScene)
3. **Add GameLifetimeScope prefab** to the scene if not already present
4. **Press Play**
5. **Press Escape** - the main menu should now appear!

## Troubleshooting

### "Instance not found! Make sure GameLifetimeScope is in the scene"

**Solution**: Drag the `GameLifetimeScope.prefab` into your scene.

### Menu still doesn't appear

**Check**:
1. GameLifetimeScope GameObject is active in the scene
2. Main Menu Visual Tree and Panel Settings are assigned in the Inspector
3. Console for any error messages during initialization

### VContainer not found

**Solution**:
1. Unity needs to download VContainer from the git URL
2. Check `Window > Package Manager` to see if it's being imported
3. May need to restart Unity after package import

## Architecture Benefits

### Before (Manual Singletons)
- ❌ Scattered singleton pattern across all managers
- ❌ Manual auto-creation logic in each manager
- ❌ MainMenuManager never instantiated
- ❌ Hard to test

### After (VContainer DI)
- ✅ Centralized dependency management
- ✅ Automatic lifecycle management
- ✅ All managers properly instantiated
- ✅ Easy to test and extend
- ✅ Clear initialization order

## Adding New Managers

To add a new manager to the DI container:

```csharp
// In GameLifetimeScope.cs Configure() method
RegisterManager<YourNewManager>(builder, "YourNewManager");
```

That's it! VContainer will handle the rest.
