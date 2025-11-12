# Settings Panel Setup Guide

This guide explains how to integrate the Settings Panel into your Unity scene.

## Overview

The settings panel provides a themed UI for managing:
- **Audio Settings**: Master, Music, and SFX volume controls
- **Graphics Settings**: Quality level, Fullscreen, and V-Sync toggles
- **Gameplay Settings**: Mouse sensitivity and Y-axis inversion

## Theme

The settings panel matches the BugWars theme:
- Dark Zinc backgrounds (Zinc 900/800)
- Lime green accents (Lime 300-700)
- Smooth animations and glowing effects
- Rounded corners and modern styling

## Setup Instructions

### 1. Create Settings Panel GameObject

1. In your scene hierarchy, find or create a Canvas with a UIDocument component
2. Create a new GameObject under the Canvas (Right-click Canvas → Create Empty)
3. Name it "SettingsPanel"
4. Add a **UIDocument** component to it
5. Add the **SettingsPanelManager** script component

### 2. Configure the UIDocument

1. Select the SettingsPanel GameObject
2. In the UIDocument component:
   - Set **Source Asset** to: `Assets/BugWars/UI/Settings/settings_panel.uxml`
   - The stylesheet is already referenced in the UXML file

### 3. Connect to Main Menu

1. Find your MainMenu GameObject (the one with MainMenuManager)
2. Select it and locate the MainMenuManager component
3. In the **Settings Panel** field, drag and drop the SettingsPanel GameObject
4. This links the Settings button to show the Settings Panel

### 4. Layer Setup (Optional)

If you want the settings panel to appear above other UI:
1. Ensure the SettingsPanel GameObject is below other UI elements in the hierarchy
2. Or adjust the Sort Order in the UIDocument component

## Usage

Once set up:
- Click the **Settings** button in the main menu to open the panel
- Click the **×** button to close the panel
- Adjust settings using sliders, toggles, and dropdowns
- Click **Apply** to save and apply changes
- Click **Reset to Default** to restore default values

## Settings Storage

Settings are saved using Unity's PlayerPrefs:
- `MasterVolume`: Master volume (0-100)
- `MusicVolume`: Music volume (0-100)
- `SFXVolume`: SFX volume (0-100)
- `QualityLevel`: Graphics quality index
- `Fullscreen`: Fullscreen mode (0/1)
- `VSync`: V-Sync enabled (0/1)
- `MouseSensitivity`: Mouse sensitivity (1-10)
- `InvertYAxis`: Y-axis inversion (0/1)

## Customization

### Adding New Settings

To add new settings:

1. **Update UXML** (`settings_panel.uxml`):
   ```xml
   <ui:VisualElement name="NewSettingContainer" class="setting-row">
       <ui:Label text="New Setting" class="setting-label" />
       <ui:Toggle name="NewSettingToggle" class="setting-toggle" />
   </ui:VisualElement>
   ```

2. **Add field in SettingsPanelManager.cs**:
   ```csharp
   private Toggle _newSettingToggle;
   ```

3. **Get reference in GetUIReferences()**:
   ```csharp
   _newSettingToggle = _root.Q<Toggle>("NewSettingToggle");
   ```

4. **Add save/load logic** in SaveSettings() and LoadSettings()

### Styling

All styles are in `settings.uss`. To modify colors, animations, or layout:
- Edit the USS file
- Changes follow the Lime/Zinc color scheme
- Maintain consistency with the main menu theme

## Troubleshooting

**Settings panel doesn't appear:**
- Ensure UIDocument component has the correct UXML file assigned
- Check that the SettingsPanelManager script is attached
- Verify the panel container starts with `display: none` in USS

**Settings button doesn't work:**
- Ensure SettingsPanelManager reference is set in MainMenuManager
- Check Console for error messages

**Settings don't save:**
- Verify PlayerPrefs has write permissions
- Check Console for save/load errors
- In WebGL builds, PlayerPrefs uses browser storage

## Files

- `settings_panel.uxml` - UI structure
- `settings.uss` - Stylesheet with Lime/Zinc theme
- `SettingsPanelManager.cs` - Logic and settings management
- `SETUP_GUIDE.md` - This guide

## Integration Points

The settings panel integrates with:
- **MainMenuManager**: Opens the panel when Settings button is clicked
- **PlayerPrefs**: Stores persistent settings
- **QualitySettings**: Applies graphics settings
- **Screen**: Applies fullscreen changes

Future integrations could include:
- Audio Manager for volume control
- Input Manager for sensitivity and axis inversion
- Language/Localization system
