using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace BugWars.Character
{
    /// <summary>
    /// Data structures for sprite atlas JSON
    /// Uses Newtonsoft.Json for dictionary support
    /// </summary>
    [Serializable]
    public class SpriteAtlasData
    {
        public AtlasMeta meta;

        [JsonProperty("frames")]
        public Dictionary<string, FrameData> frames;

        [JsonProperty("animations")]
        public Dictionary<string, AnimationData> animations;

        /// <summary>
        /// Load from JSON string using Newtonsoft.Json
        /// </summary>
        public static SpriteAtlasData FromJson(string json)
        {
            return JsonConvert.DeserializeObject<SpriteAtlasData>(json);
        }
    }

    [Serializable]
    public class AtlasMeta
    {
        public string version;
        public SizeData size;
        public int frameSize;
        public int frameCount;
    }

    [Serializable]
    public class SizeData
    {
        public int w;
        public int h;
    }

    [Serializable]
    public class FrameData
    {
        public int x;
        public int y;
        public int w;
        public int h;
        public string animation;
        public int index;
        public UVData uv;
    }

    [Serializable]
    public class UVData
    {
        public Vector2Data min;
        public Vector2Data max;

        public Vector2 GetMin() => new Vector2(min.x, min.y);
        public Vector2 GetMax() => new Vector2(max.x, max.y);
    }

    [Serializable]
    public class Vector2Data
    {
        public float x;
        public float y;
    }

    [Serializable]
    public class AnimationData
    {
        public List<string> frames;
        public int frameCount;
        public int fps;

        public float GetFrameDuration() => 1f / fps;
    }
}
