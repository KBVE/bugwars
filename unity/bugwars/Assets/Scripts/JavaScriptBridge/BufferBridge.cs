using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace BugWars.JavaScriptBridge
{
    /// <summary>
    /// Handles binary data and typed array transfers between Unity and JavaScript.
    /// Supports efficient transfer of large data arrays (mesh data, terrain heightmaps, etc.)
    /// </summary>
    public static class BufferBridge
    {
        #region External JavaScript Functions

#if UNITY_WEBGL && !UNITY_EDITOR
        // JavaScript functions for efficient buffer transfer
        [DllImport("__Internal")]
        private static extern void SendFloat32Array(string eventType, float[] data, int length);

        [DllImport("__Internal")]
        private static extern void SendInt32Array(string eventType, int[] data, int length);

        [DllImport("__Internal")]
        private static extern void SendUint8Array(string eventType, byte[] data, int length);

        [DllImport("__Internal")]
        private static extern void SendFloat64Array(string eventType, double[] data, int length);
#endif

        #endregion

        #region Array Data Transfer

        /// <summary>
        /// Send a float array to JavaScript as Float32Array.
        /// Efficient for mesh vertices, terrain heightmaps, etc.
        /// </summary>
        public static void SendFloatArray(string eventType, float[] data)
        {
            if (data == null || data.Length == 0)
            {
                Debug.LogWarning("[BufferBridge] Attempted to send null or empty float array");
                return;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                SendFloat32Array(eventType, data, data.Length);
                Debug.Log($"[BufferBridge] Sent Float32Array: {data.Length} elements");
            }
            catch (Exception e)
            {
                Debug.LogError($"[BufferBridge] Error sending float array: {e.Message}");
            }
#else
            Debug.Log($"[BufferBridge] (Editor Mode) Would send Float32Array: {data.Length} elements");
#endif
        }

        /// <summary>
        /// Send an int array to JavaScript as Int32Array.
        /// Efficient for mesh triangles, IDs, indices, etc.
        /// </summary>
        public static void SendIntArray(string eventType, int[] data)
        {
            if (data == null || data.Length == 0)
            {
                Debug.LogWarning("[BufferBridge] Attempted to send null or empty int array");
                return;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                SendInt32Array(eventType, data, data.Length);
                Debug.Log($"[BufferBridge] Sent Int32Array: {data.Length} elements");
            }
            catch (Exception e)
            {
                Debug.LogError($"[BufferBridge] Error sending int array: {e.Message}");
            }
#else
            Debug.Log($"[BufferBridge] (Editor Mode) Would send Int32Array: {data.Length} elements");
#endif
        }

        /// <summary>
        /// Send a byte array to JavaScript as Uint8Array.
        /// Efficient for binary data, textures, compressed data, etc.
        /// </summary>
        public static void SendByteArray(string eventType, byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                Debug.LogWarning("[BufferBridge] Attempted to send null or empty byte array");
                return;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                SendUint8Array(eventType, data, data.Length);
                Debug.Log($"[BufferBridge] Sent Uint8Array: {data.Length} bytes");
            }
            catch (Exception e)
            {
                Debug.LogError($"[BufferBridge] Error sending byte array: {e.Message}");
            }
#else
            Debug.Log($"[BufferBridge] (Editor Mode) Would send Uint8Array: {data.Length} bytes");
#endif
        }

        /// <summary>
        /// Send a double array to JavaScript as Float64Array.
        /// </summary>
        public static void SendDoubleArray(string eventType, double[] data)
        {
            if (data == null || data.Length == 0)
            {
                Debug.LogWarning("[BufferBridge] Attempted to send null or empty double array");
                return;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                SendFloat64Array(eventType, data, data.Length);
                Debug.Log($"[BufferBridge] Sent Float64Array: {data.Length} elements");
            }
            catch (Exception e)
            {
                Debug.LogError($"[BufferBridge] Error sending double array: {e.Message}");
            }
#else
            Debug.Log($"[BufferBridge] (Editor Mode) Would send Float64Array: {data.Length} elements");
#endif
        }

        #endregion

        #region Base64 Encoding/Decoding

        /// <summary>
        /// Encode byte array to Base64 string for JSON transfer.
        /// Use this when you need to send binary data through JSON.
        /// </summary>
        public static string EncodeToBase64(byte[] data)
        {
            if (data == null || data.Length == 0)
                return string.Empty;

            try
            {
                return Convert.ToBase64String(data);
            }
            catch (Exception e)
            {
                Debug.LogError($"[BufferBridge] Base64 encoding error: {e.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Decode Base64 string to byte array.
        /// Use this to receive binary data from JavaScript through JSON.
        /// </summary>
        public static byte[] DecodeFromBase64(string base64)
        {
            if (string.IsNullOrEmpty(base64))
                return Array.Empty<byte>();

            try
            {
                return Convert.FromBase64String(base64);
            }
            catch (Exception e)
            {
                Debug.LogError($"[BufferBridge] Base64 decoding error: {e.Message}");
                return Array.Empty<byte>();
            }
        }

        #endregion

        #region String Encoding

        /// <summary>
        /// Encode string to UTF-8 byte array.
        /// </summary>
        public static byte[] EncodeStringToBytes(string text)
        {
            if (string.IsNullOrEmpty(text))
                return Array.Empty<byte>();

            return Encoding.UTF8.GetBytes(text);
        }

        /// <summary>
        /// Decode UTF-8 byte array to string.
        /// </summary>
        public static string DecodeBytesToString(byte[] data)
        {
            if (data == null || data.Length == 0)
                return string.Empty;

            return Encoding.UTF8.GetString(data);
        }

        #endregion

        #region Mesh Data Transfer

        /// <summary>
        /// Send mesh data to JavaScript (vertices, triangles, normals, UVs).
        /// Useful for procedurally generated meshes that need to be visualized on web.
        /// </summary>
        public static void SendMeshData(string eventType, Mesh mesh)
        {
            if (mesh == null)
            {
                Debug.LogWarning("[BufferBridge] Attempted to send null mesh");
                return;
            }

            var meshData = new MeshDataPacket
            {
                name = mesh.name,
                vertexCount = mesh.vertexCount,
                triangleCount = mesh.triangles.Length / 3
            };

            // Send mesh metadata first
            WebGLBridge.SendToWeb($"{eventType}_Metadata", meshData);

            // Then send the actual data arrays
            SendFloatArray($"{eventType}_Vertices", ConvertVector3ArrayToFloats(mesh.vertices));
            SendIntArray($"{eventType}_Triangles", mesh.triangles);

            if (mesh.normals != null && mesh.normals.Length > 0)
                SendFloatArray($"{eventType}_Normals", ConvertVector3ArrayToFloats(mesh.normals));

            if (mesh.uv != null && mesh.uv.Length > 0)
                SendFloatArray($"{eventType}_UVs", ConvertVector2ArrayToFloats(mesh.uv));

            Debug.Log($"[BufferBridge] Sent mesh data: {mesh.name} ({mesh.vertexCount} vertices)");
        }

        /// <summary>
        /// Convert Vector3 array to flat float array (x,y,z,x,y,z,...).
        /// </summary>
        public static float[] ConvertVector3ArrayToFloats(Vector3[] vectors)
        {
            float[] result = new float[vectors.Length * 3];
            for (int i = 0; i < vectors.Length; i++)
            {
                result[i * 3] = vectors[i].x;
                result[i * 3 + 1] = vectors[i].y;
                result[i * 3 + 2] = vectors[i].z;
            }
            return result;
        }

        /// <summary>
        /// Convert flat float array to Vector3 array.
        /// </summary>
        public static Vector3[] ConvertFloatsToVector3Array(float[] floats)
        {
            if (floats.Length % 3 != 0)
            {
                Debug.LogError("[BufferBridge] Float array length must be multiple of 3 for Vector3 conversion");
                return Array.Empty<Vector3>();
            }

            Vector3[] result = new Vector3[floats.Length / 3];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = new Vector3(
                    floats[i * 3],
                    floats[i * 3 + 1],
                    floats[i * 3 + 2]
                );
            }
            return result;
        }

        /// <summary>
        /// Convert Vector2 array to flat float array (x,y,x,y,...).
        /// </summary>
        public static float[] ConvertVector2ArrayToFloats(Vector2[] vectors)
        {
            float[] result = new float[vectors.Length * 2];
            for (int i = 0; i < vectors.Length; i++)
            {
                result[i * 2] = vectors[i].x;
                result[i * 2 + 1] = vectors[i].y;
            }
            return result;
        }

        /// <summary>
        /// Convert flat float array to Vector2 array.
        /// </summary>
        public static Vector2[] ConvertFloatsToVector2Array(float[] floats)
        {
            if (floats.Length % 2 != 0)
            {
                Debug.LogError("[BufferBridge] Float array length must be multiple of 2 for Vector2 conversion");
                return Array.Empty<Vector2>();
            }

            Vector2[] result = new Vector2[floats.Length / 2];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = new Vector2(
                    floats[i * 2],
                    floats[i * 2 + 1]
                );
            }
            return result;
        }

        #endregion

        #region Terrain Data Transfer

        /// <summary>
        /// Send terrain heightmap data to JavaScript.
        /// Efficient for large terrain chunks.
        /// </summary>
        public static void SendTerrainHeightmap(string eventType, int chunkX, int chunkZ, float[,] heightMap)
        {
            if (heightMap == null)
            {
                Debug.LogWarning("[BufferBridge] Attempted to send null heightmap");
                return;
            }

            int width = heightMap.GetLength(0);
            int height = heightMap.GetLength(1);

            // Flatten 2D array to 1D
            float[] flatHeightMap = new float[width * height];
            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < height; z++)
                {
                    flatHeightMap[x * height + z] = heightMap[x, z];
                }
            }

            // Send metadata
            var metadata = new TerrainChunkMetadata
            {
                chunkX = chunkX,
                chunkZ = chunkZ,
                width = width,
                height = height
            };
            WebGLBridge.SendToWeb($"{eventType}_Metadata", metadata);

            // Send heightmap data
            SendFloatArray($"{eventType}_Heightmap", flatHeightMap);

            Debug.Log($"[BufferBridge] Sent terrain heightmap: Chunk({chunkX},{chunkZ}) Size:{width}x{height}");
        }

        /// <summary>
        /// Convert 1D float array back to 2D heightmap.
        /// </summary>
        public static float[,] ConvertFlatArrayToHeightmap(float[] flatArray, int width, int height)
        {
            if (flatArray.Length != width * height)
            {
                Debug.LogError($"[BufferBridge] Array size mismatch: expected {width * height}, got {flatArray.Length}");
                return new float[0, 0];
            }

            float[,] heightMap = new float[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < height; z++)
                {
                    heightMap[x, z] = flatArray[x * height + z];
                }
            }
            return heightMap;
        }

        #endregion

        #region Texture Data Transfer

        /// <summary>
        /// Send texture data to JavaScript as byte array.
        /// </summary>
        public static void SendTextureData(string eventType, Texture2D texture)
        {
            if (texture == null)
            {
                Debug.LogWarning("[BufferBridge] Attempted to send null texture");
                return;
            }

            // Get PNG encoded texture
            byte[] textureData = texture.EncodeToPNG();

            var metadata = new TextureMetadata
            {
                name = texture.name,
                width = texture.width,
                height = texture.height,
                format = texture.format.ToString()
            };

            WebGLBridge.SendToWeb($"{eventType}_Metadata", metadata);
            SendByteArray($"{eventType}_Data", textureData);

            Debug.Log($"[BufferBridge] Sent texture: {texture.name} ({texture.width}x{texture.height})");
        }

        #endregion
    }

    #region Buffer Data Structures

    [Serializable]
    public class MeshDataPacket
    {
        public string name;
        public int vertexCount;
        public int triangleCount;
    }

    [Serializable]
    public class TerrainChunkMetadata
    {
        public int chunkX;
        public int chunkZ;
        public int width;
        public int height;
    }

    [Serializable]
    public class TextureMetadata
    {
        public string name;
        public int width;
        public int height;
        public string format;
    }

    #endregion
}
