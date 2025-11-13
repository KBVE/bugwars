# Socials UI - Modular Social Media Links Component

A flexible, modular UI component for displaying social media links (Discord, Twitch, etc.) in your Unity game.

## Features

- **Modular & Reusable** - Easy to instantiate anywhere in your project
- **Configurable** - Add/remove social links dynamically
- **Platform Support** - Built-in support for Discord, Twitch, Twitter, YouTube
- **Customizable Colors** - Set custom colors for each platform
- **Flexible Layout** - Horizontal or vertical button arrangement
- **Consistent Styling** - Matches BugWars UI theme (Lime & Zinc colors)

## Quick Setup

### Method 1: Add to Existing Scene

1. **Create GameObject**
   - Right-click in Hierarchy → Create Empty
   - Name it "SocialsUI"

2. **Add Components**
   - Add Component → UI Toolkit → UI Document
   - Add Component → Scripts → BugWars.UI → SocialsManager

3. **Configure UI Document**
   - Assign `socials.uxml` to the **Source Asset** field
   - (Optional) Assign Panel Settings if needed

4. **Configure Social Links**
   - In SocialsManager component, expand **Social Links Configuration**
   - Click "+" to add social links
   - Set Platform Name, URL, and colors for each

5. **Adjust Settings**
   - **Horizontal Layout**: Check for horizontal buttons, uncheck for vertical
   - **Show On Start**: Check to display panel immediately

### Method 2: Use from Code

```csharp
using BugWars.UI;
using UnityEngine;
using UnityEngine.UIElements;

public class MyGameManager : MonoBehaviour
{
    void Start()
    {
        // Create GameObject
        GameObject socialsObj = new GameObject("SocialsUI");

        // Add UIDocument
        var uiDoc = socialsObj.AddComponent<UIDocument>();
        uiDoc.visualTreeAsset = Resources.Load<VisualTreeAsset>("socials");

        // Add SocialsManager
        var socialsManager = socialsObj.AddComponent<SocialsManager>();

        // Add social links programmatically
        socialsManager.AddSocialLink(SocialLink.CreateDiscordLink("https://discord.gg/yourinvite"));
        socialsManager.AddSocialLink(SocialLink.CreateTwitchLink("https://twitch.tv/yourchannel"));

        // Show the panel
        socialsManager.ShowPanel();
    }
}
```

## Using Factory Methods

The `SocialLink` class provides convenient factory methods:

```csharp
// Discord
var discord = SocialLink.CreateDiscordLink("https://discord.gg/example");

// Twitch
var twitch = SocialLink.CreateTwitchLink("https://twitch.tv/example");

// Twitter/X
var twitter = SocialLink.CreateTwitterLink("https://twitter.com/example");

// YouTube
var youtube = SocialLink.CreateYouTubeLink("https://youtube.com/@example");
```

## Custom Social Links

Create custom social links with your own colors:

```csharp
var customLink = new SocialLink(
    platformName: "Instagram",
    url: "https://instagram.com/example",
    buttonColor: "#E4405F",  // Instagram pink
    hoverColor: "#F56692"    // Lighter pink
);

socialsManager.AddSocialLink(customLink);
```

## Public API

### SocialsManager Methods

```csharp
// Visibility
void ShowPanel()                      // Show the socials panel
void HidePanel()                      // Hide the socials panel
void TogglePanel()                    // Toggle visibility

// Managing Links
void AddSocialLink(SocialLink link)   // Add a social link
void RemoveSocialLink(string name)    // Remove link by platform name
void ClearAllLinks()                  // Remove all links

// Properties
bool IsPanelVisible                   // Check if panel is visible
List<SocialLink> SocialLinks         // Get/set all social links
```

### SocialLink Properties

```csharp
string platformName    // Platform name (e.g., "Discord")
string url            // URL to open
Sprite icon           // Optional icon sprite
string buttonColor    // Hex color for button (e.g., "#5865F2")
string hoverColor     // Hex color for hover state
```

## Examples

### Example 1: Main Menu Integration

```csharp
public class MainMenuManager : MonoBehaviour
{
    [SerializeField] private SocialsManager _socialsManager;

    public void OnSocialsButtonClicked()
    {
        _socialsManager.ShowPanel();
    }
}
```

### Example 2: Dynamic Social Links

```csharp
public class DynamicSocialsExample : MonoBehaviour
{
    private SocialsManager _socialsManager;

    void Start()
    {
        _socialsManager = GetComponent<SocialsManager>();

        // Clear default links
        _socialsManager.ClearAllLinks();

        // Add links based on game state or player preferences
        if (PlayerPrefs.GetInt("ShowDiscord", 1) == 1)
        {
            _socialsManager.AddSocialLink(
                SocialLink.CreateDiscordLink("https://discord.gg/example")
            );
        }

        if (PlayerPrefs.GetInt("ShowTwitch", 1) == 1)
        {
            _socialsManager.AddSocialLink(
                SocialLink.CreateTwitchLink("https://twitch.tv/example")
            );
        }
    }
}
```

### Example 3: Context Menu Testing (Editor Only)

Right-click on SocialsManager component in Inspector:
- **Add Default Links (Discord & Twitch)** - Quickly add test links
- **Toggle Panel** - Test visibility toggle

## Customization

### Changing Position

The socials panel is positioned bottom-right by default. To change:

1. Edit `socials.uss`
2. Modify `.socials-container`:
```css
.socials-container {
    position: absolute;
    bottom: 20px;   /* Change these values */
    right: 20px;    /* to reposition */
}
```

### Changing Colors

Colors can be customized per-link via the Inspector or code, or globally by editing `socials.uss`.

### Layout Direction

Toggle between horizontal and vertical layouts:
- **Inspector**: Check/uncheck "Horizontal Layout"
- **Code**: `socialsManager._horizontalLayout = false;`

## Files

- `SocialLink.cs` - Data class for social media links
- `SocialsManager.cs` - Main manager component
- `socials.uxml` - UI layout structure
- `socials.uss` - Styling and animations
- `README.md` - This documentation

## Notes

- Buttons automatically open URLs using `Application.OpenURL()`
- Panel supports show/hide animations via USS transitions
- All buttons have hover and active states for better UX
- Compatible with Unity UI Toolkit (UIElements)

## Troubleshooting

**Buttons not appearing:**
- Ensure social links are added in Inspector or via code
- Check Console for errors about missing UXML/USS

**Colors not working:**
- Verify color format is hex (e.g., "#5865F2")
- Colors are case-insensitive

**Panel not showing:**
- Check "Show On Start" is enabled, or call `ShowPanel()` manually
- Verify UIDocument has `socials.uxml` assigned

## Support

For issues or questions, refer to:
- Main UI documentation: `Assets/BugWars/UI/UI_INTEGRATION_GUIDE.md`
- Quick fixes: `Assets/BugWars/UI/QUICK_FIX_GUIDE.md`
