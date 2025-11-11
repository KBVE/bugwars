using System;
using System.Collections.Generic;
using UnityEngine;

namespace BugWars.JavaScriptBridge
{
    /// <summary>
    /// Helper utilities for JSON serialization and deserialization between Unity and JavaScript.
    /// Provides enhanced JSON handling beyond Unity's basic JsonUtility.
    /// </summary>
    public static class JSONBridge
    {
        /// <summary>
        /// Serialize an object to JSON string.
        /// Uses Unity's JsonUtility with pretty print option.
        /// </summary>
        public static string Serialize(object obj, bool prettyPrint = false)
        {
            try
            {
                return JsonUtility.ToJson(obj, prettyPrint);
            }
            catch (Exception e)
            {
                Debug.LogError($"[JSONBridge] Serialization error: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// Deserialize JSON string to object of type T.
        /// </summary>
        public static T Deserialize<T>(string json)
        {
            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[JSONBridge] Deserialization error: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// Safely try to deserialize JSON, returns default if fails.
        /// </summary>
        public static bool TryDeserialize<T>(string json, out T result)
        {
            try
            {
                result = JsonUtility.FromJson<T>(json);
                return result != null;
            }
            catch
            {
                result = default(T);
                return false;
            }
        }

        /// <summary>
        /// Serialize a Vector3 to a JSON-compatible object.
        /// </summary>
        public static string SerializeVector3(Vector3 vector)
        {
            return JsonUtility.ToJson(new Vector3Data
            {
                x = vector.x,
                y = vector.y,
                z = vector.z
            });
        }

        /// <summary>
        /// Deserialize JSON to Vector3.
        /// </summary>
        public static Vector3 DeserializeVector3(string json)
        {
            var data = JsonUtility.FromJson<Vector3Data>(json);
            return new Vector3(data.x, data.y, data.z);
        }

        /// <summary>
        /// Serialize a Quaternion to JSON.
        /// </summary>
        public static string SerializeQuaternion(Quaternion rotation)
        {
            return JsonUtility.ToJson(new QuaternionData
            {
                x = rotation.x,
                y = rotation.y,
                z = rotation.z,
                w = rotation.w
            });
        }

        /// <summary>
        /// Deserialize JSON to Quaternion.
        /// </summary>
        public static Quaternion DeserializeQuaternion(string json)
        {
            var data = JsonUtility.FromJson<QuaternionData>(json);
            return new Quaternion(data.x, data.y, data.z, data.w);
        }

        /// <summary>
        /// Serialize a Transform to JSON.
        /// </summary>
        public static string SerializeTransform(Transform transform)
        {
            return JsonUtility.ToJson(new TransformData
            {
                position = new Vector3Data
                {
                    x = transform.position.x,
                    y = transform.position.y,
                    z = transform.position.z
                },
                rotation = new QuaternionData
                {
                    x = transform.rotation.x,
                    y = transform.rotation.y,
                    z = transform.rotation.z,
                    w = transform.rotation.w
                },
                scale = new Vector3Data
                {
                    x = transform.localScale.x,
                    y = transform.localScale.y,
                    z = transform.localScale.z
                }
            });
        }

        /// <summary>
        /// Apply JSON transform data to a Transform component.
        /// </summary>
        public static void ApplyTransformData(Transform transform, string json)
        {
            var data = JsonUtility.FromJson<TransformData>(json);
            transform.position = new Vector3(data.position.x, data.position.y, data.position.z);
            transform.rotation = new Quaternion(data.rotation.x, data.rotation.y, data.rotation.z, data.rotation.w);
            transform.localScale = new Vector3(data.scale.x, data.scale.y, data.scale.z);
        }

        /// <summary>
        /// Serialize a list of objects (Unity's JsonUtility doesn't support lists directly).
        /// </summary>
        public static string SerializeList<T>(List<T> list)
        {
            var wrapper = new ListWrapper<T> { items = list };
            return JsonUtility.ToJson(wrapper);
        }

        /// <summary>
        /// Deserialize a JSON array to a List.
        /// </summary>
        public static List<T> DeserializeList<T>(string json)
        {
            var wrapper = JsonUtility.FromJson<ListWrapper<T>>(json);
            return wrapper.items;
        }

        /// <summary>
        /// Serialize a dictionary to JSON (converts to list of key-value pairs).
        /// </summary>
        public static string SerializeDictionary<TKey, TValue>(Dictionary<TKey, TValue> dict)
        {
            var list = new List<KeyValuePairData<TKey, TValue>>();
            foreach (var kvp in dict)
            {
                list.Add(new KeyValuePairData<TKey, TValue> { key = kvp.Key, value = kvp.Value });
            }
            return SerializeList(list);
        }

        /// <summary>
        /// Deserialize JSON to Dictionary.
        /// </summary>
        public static Dictionary<TKey, TValue> DeserializeDictionary<TKey, TValue>(string json)
        {
            var list = DeserializeList<KeyValuePairData<TKey, TValue>>(json);
            var dict = new Dictionary<TKey, TValue>();
            foreach (var item in list)
            {
                dict[item.key] = item.value;
            }
            return dict;
        }

        /// <summary>
        /// Create a standardized response object for sending to JavaScript.
        /// </summary>
        public static string CreateResponse(bool success, string message = null, object data = null)
        {
            return JsonUtility.ToJson(new ResponseData
            {
                success = success,
                message = message ?? string.Empty,
                data = data != null ? JsonUtility.ToJson(data) : string.Empty,
                timestamp = DateTime.UtcNow.ToString("o")
            });
        }

        /// <summary>
        /// Parse a response from JavaScript.
        /// </summary>
        public static ResponseData ParseResponse(string json)
        {
            return JsonUtility.FromJson<ResponseData>(json);
        }
    }

    #region JSON Data Structures

    [Serializable]
    public class Vector3Data
    {
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    public class QuaternionData
    {
        public float x;
        public float y;
        public float z;
        public float w;
    }

    [Serializable]
    public class TransformData
    {
        public Vector3Data position;
        public QuaternionData rotation;
        public Vector3Data scale;
    }

    [Serializable]
    public class ListWrapper<T>
    {
        public List<T> items;
    }

    [Serializable]
    public class KeyValuePairData<TKey, TValue>
    {
        public TKey key;
        public TValue value;
    }

    [Serializable]
    public class ResponseData
    {
        public bool success;
        public string message;
        public string data;
        public string timestamp;
    }

    #endregion

    #region Common Game Data Structures

    /// <summary>
    /// Player data structure for synchronization with JavaScript.
    /// </summary>
    [Serializable]
    public class PlayerData
    {
        public string playerId;
        public string playerName;
        public int level;
        public int experience;
        public int health;
        public int maxHealth;
        public Vector3Data position;
        public string[] inventory;
        public Dictionary<string, float> stats;
    }

    /// <summary>
    /// Game state data for persistence.
    /// </summary>
    [Serializable]
    public class GameStateData
    {
        public string gameId;
        public string sessionId;
        public int currentLevel;
        public float playTime;
        public string lastSaveTime;
        public string gameMode;
        public bool isPaused;
    }

    /// <summary>
    /// Entity data for synchronization.
    /// </summary>
    [Serializable]
    public class EntityData
    {
        public string entityId;
        public string entityType;
        public Vector3Data position;
        public QuaternionData rotation;
        public int health;
        public bool isActive;
    }

    /// <summary>
    /// Chunk data for terrain synchronization.
    /// </summary>
    [Serializable]
    public class ChunkData
    {
        public int chunkX;
        public int chunkZ;
        public bool isLoaded;
        public float[] heightMap;
        public int[] biomeIds;
    }

    #endregion
}
