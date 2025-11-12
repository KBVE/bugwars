# QUICK FIX: Settings Button Not Working

## Problem
Clicking "Settings" shows: `[MainMenuManager] SettingsPanelManager reference not set!`

## Root Cause
The `settingsPanelVisualTree` field is not assigned in the Unity Inspector.

## Solution (2 minutes)

### Step 1: Run Diagnostics
1. In Unity, add the `DiagnosticChecker` component to any GameObject
2. Press Play
3. Check the Console for detailed error messages
4. The diagnostics will tell you exactly what's missing

### Step 2: Assign UXML Files in Inspector

1. **Find GameLifetimeScope GameObject**
   - Look in your Scene Hierarchy
   - Usually at the root level or under a "Managers" folder

2. **Select GameLifetimeScope**
   - Click on it in the Hierarchy

3. **In the Inspector, find "UI Configuration" section**

4. **Assign these fields by dragging from Project window:**

   | Field Name | Path to Drag |
   |------------|--------------|
   | **Main Menu Visual Tree** | `Assets/BugWars/UI/MainMenu/main_menu.uxml` |
   | **Settings Panel Visual Tree** | `Assets/BugWars/UI/Settings/settings_panel.uxml` |
   | **Panel Settings** (optional) | `Assets/BugWars/UI/MainMenu/MainMenuPanelSettings.asset` |

5. **Press Play** - Settings button should now work!

## How to Assign in Unity Inspector

### Method 1: Drag and Drop
1. In Project window, navigate to `Assets/BugWars/UI/Settings/`
2. Find `settings_panel.uxml`
3. Drag it onto the **Settings Panel Visual Tree** field in Inspector
4. Repeat for `main_menu.uxml`

### Method 2: Click the Circle Icon
1. Click the small circle icon (⊙) next to the field
2. Search for "settings_panel"
3. Double-click to assign

## Verification

After assigning, you should see in Console (when you press Play):
```
[GameLifetimeScope] SettingsPanelManager created with sort order 200
[GameLifetimeScope] SettingsPanelManager reference assigned to MainMenuManager
```

If you see:
```
[GameLifetimeScope] SettingsPanel VisualTreeAsset not assigned!
```
Then the field is still not assigned - go back to Step 2.

## Still Not Working?

1. Check Console for errors starting with `[GameLifetimeScope]`
2. Verify the UXML files exist at the paths above
3. Make sure GameLifetimeScope GameObject is active in the scene
4. Try deleting and re-creating the GameLifetimeScope GameObject

## Visual Reference

Your Inspector should look like this:

```
GameLifetimeScope (Script)
├── UI Configuration
│   ├── Main Menu Visual Tree: main_menu (VisualTreeAsset)
│   ├── Settings Panel Visual Tree: settings_panel (VisualTreeAsset)  ← MUST BE ASSIGNED!
│   └── Panel Settings: MainMenuPanelSettings (PanelSettings)
└── Manager References
    └── ...
```

## Alternative: Manual Wiring (If Above Doesn't Work)

If the automatic wiring doesn't work, you can manually set it up:

1. Create GameObject named "SettingsPanel"
2. Add UIDocument component
3. Assign `settings_panel.uxml` to Source Asset
4. Add SettingsPanelManager script
5. Find MainMenuManager GameObject
6. Drag SettingsPanel GameObject to the "Settings Panel Manager" field

But the automatic method above should work if you assign the UXML files correctly!
