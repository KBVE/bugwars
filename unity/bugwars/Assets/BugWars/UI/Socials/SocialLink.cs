using UnityEngine;

namespace BugWars.UI
{
    /// <summary>
    /// Data class representing a social media link
    /// Used to configure social buttons in the SocialsManager
    /// </summary>
    [System.Serializable]
    public class SocialLink
    {
        [Header("Social Link Configuration")]
        [Tooltip("Name of the social platform (e.g., Discord, Twitch)")]
        public string platformName = "Discord";

        [Tooltip("URL to open when the button is clicked")]
        public string url = "https://discord.gg/example";

        [Tooltip("Icon sprite for the social button (optional)")]
        public Sprite icon;

        [Tooltip("Background color for the button (hex format: #RRGGBB)")]
        public string buttonColor = "#5865F2"; // Discord blue by default

        [Tooltip("Hover color for the button (hex format: #RRGGBB)")]
        public string hoverColor = "#7289DA";

        /// <summary>
        /// Creates a new SocialLink with default values
        /// </summary>
        public SocialLink()
        {
        }

        /// <summary>
        /// Creates a new SocialLink with specified values
        /// </summary>
        public SocialLink(string platformName, string url, string buttonColor = "#5865F2", string hoverColor = "#7289DA")
        {
            this.platformName = platformName;
            this.url = url;
            this.buttonColor = buttonColor;
            this.hoverColor = hoverColor;
        }

        /// <summary>
        /// Factory method for creating a Discord social link
        /// </summary>
        public static SocialLink CreateDiscordLink(string inviteUrl)
        {
            return new SocialLink("Discord", inviteUrl, "#5865F2", "#7289DA");
        }

        /// <summary>
        /// Factory method for creating a Twitch social link
        /// </summary>
        public static SocialLink CreateTwitchLink(string channelUrl)
        {
            return new SocialLink("Twitch", channelUrl, "#9146FF", "#B880FF");
        }

        /// <summary>
        /// Factory method for creating a Twitter/X social link
        /// </summary>
        public static SocialLink CreateTwitterLink(string profileUrl)
        {
            return new SocialLink("Twitter", profileUrl, "#1DA1F2", "#58B5F5");
        }

        /// <summary>
        /// Factory method for creating a YouTube social link
        /// </summary>
        public static SocialLink CreateYouTubeLink(string channelUrl)
        {
            return new SocialLink("YouTube", channelUrl, "#FF0000", "#FF3333");
        }
    }
}
