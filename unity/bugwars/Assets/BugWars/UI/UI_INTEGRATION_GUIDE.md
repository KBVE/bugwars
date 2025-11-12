# UI Integration Guide for BugWars

## Quick Setup Instructions

The UI system is automatically created at runtime by the `GameLifetimeScope`. You only need to configure the references in the Unity Inspector.

### Step 1: Configure GameLifetimeScope

1. Find the **GameLifetimeScope** GameObject in your scene (or create one if it doesn't exist)
2. In the Inspector, locate the **UI Configuration** section
3. Assign the following fields:

   - **Main Menu Visual Tree**: `Assets/BugWars/UI/MainMenu/main_menu.uxml`
   - **Settings Panel Visual Tree**: `Assets/BugWars/UI/Settings/settings_panel.uxml`
   - **Panel Settings** (Optional): `Assets/BugWars/UI/MainMenu/MainMenuPanelSettings.asset`

### Step 2: Test in Play Mode

1. Press Play in Unity
2. The Main Menu and Settings Panel will be automatically created
3. Click the "Settings" button to open the settings panel
4. The settings panel will appear on top (z-index 200 vs 100)

## How It Works

### Automatic UI Creation

The `GameLifetimeScope` automatically creates both UI panels at runtime:

- **MainMenuManager** - Sort Order: 100 (bottom layer)
- **SettingsPanelManager** - Sort Order: 200 (top layer, appears above main menu)

### Z-Index / Layer Ordering

The settings panel is configured with a **higher sort order (200)** than the main menu (100), ensuring it always appears on top when opened.

### Wiring

The `GameLifetimeScope` automatically:
1. Creates the SettingsPanelManager first
2. Creates the MainMenuManager second
3. Uses reflection to assign the SettingsPanelManager reference to MainMenuManager
4. Registers both with VContainer for dependency injection

## Troubleshooting

**Settings button does nothing:**
- Ensure `settingsPanelVisualTree` is assigned in GameLifetimeScope Inspector
- Check Console for error messages about missing UXML assets
- Verify both UXML files exist in the correct paths

**Settings panel appears behind main menu:**
- This should not happen as settings has sort order 200 vs 100
- Check Console logs to verify sort orders are being set correctly

**UI doesn't appear at all:**
- Ensure `mainMenuVisualTree` is assigned in GameLifetimeScope Inspector
- Check that GameLifetimeScope GameObject is active in the scene
- Verify PanelSettings is assigned (or let it create default)

## Manual Setup (Alternative)

If you prefer to set up UI manually without GameLifetimeScope:

1. Create a GameObject with UIDocument component
2. Assign the UXML file to the UIDocument
3. Add the corresponding Manager script (MainMenuManager or SettingsPanelManager)
4. Set Sort Order: MainMenu=100, Settings=200
5. Link SettingsPanelManager reference in MainMenuManager Inspector

## Files

- `GameLifetimeScope.cs` - Automatic UI creation and wiring
- `main_menu.uxml` - Main menu UI structure
- `settings_panel.uxml` - Settings panel UI structure
- `MainMenuManager.cs` - Main menu logic
- `SettingsPanelManager.cs` - Settings panel logic

## Notes

- Both UI elements use `DontDestroyOnLoad()` to persist across scenes
- Settings are saved using PlayerPrefs
- The settings panel is hidden by default (`display: none` in USS)
- Clicking Settings button calls `ShowPanel()` on SettingsPanelManager
