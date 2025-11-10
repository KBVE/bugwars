# KBVE Editor Utilities

Custom editor tools for the BugWars project, accessible from Unity's top menu bar under **KBVE**.

## Menu Structure

### KBVE / UI Toolkit
- **Create Panel Settings** - Creates a PanelSettings asset in the currently selected folder
- **Create Main Menu Panel Settings** - Creates a PanelSettings asset specifically for MainMenu at `Assets/BugWars/UI/MainMenu/`

### KBVE / Quick Actions
- **Clear Console (F1)** - Clears the Unity Console
- **Refresh Assets (F5)** - Refreshes the Asset Database
- **Reimport All Assets** - Reimports all assets (use with caution)
- **Delete PlayerPrefs** - Deletes all saved PlayerPrefs data

### KBVE / Scene Management
- **Save Scene (Alt+S)** - Saves all open scenes
- **New Scene** - Creates a new empty scene

### KBVE / Debug
- **Log System Info** - Logs system information to the console

## Files

- `CreatePanelSettings.cs` - UI Toolkit utilities
- `KBVEMenuItems.cs` - General editor menu items
- `README.md` - This file

## Notes

All scripts are wrapped in `#if UNITY_EDITOR` preprocessor directives to ensure they only compile in the Unity Editor and not in builds.
