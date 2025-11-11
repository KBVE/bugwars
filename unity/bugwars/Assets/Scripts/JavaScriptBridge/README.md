# JavaScript Bridge for Unity WebGL

This folder contains the JavaScript bridge system for bi-directional communication between Unity WebGL and React/JavaScript.

## Architecture Overview

```
┌─────────────────────────────────────────────────┐
│           React/Astro Frontend                  │
│  (unityService.ts + ReactUnity.tsx)             │
└──────────────────┬──────────────────────────────┘
                   │
                   │ react-unity-webgl
                   │
┌──────────────────▼──────────────────────────────┐
│         Unity WebGL Build                       │
│  ┌───────────────────────────────────────────┐  │
│  │  WebGLBridge.cs (GameObject)              │  │
│  │  - Receives messages from JavaScript      │  │
│  │  - Sends messages to JavaScript           │  │
│  └───────────────────────────────────────────┘  │
│                                                  │
│  ┌───────────────────────────────────────────┐  │
│  │  JSONBridge.cs (Static Utilities)        │  │
│  │  - JSON serialization/deserialization     │  │
│  │  - Vector/Transform helpers               │  │
│  └───────────────────────────────────────────┘  │
│                                                  │
│  ┌───────────────────────────────────────────┐  │
│  │  BufferBridge.cs (Static Utilities)      │  │
│  │  - Binary data transfer                   │  │
│  │  - Typed array transfer (Float32, Int32)  │  │
│  │  - Mesh/Terrain data transfer             │  │
│  └───────────────────────────────────────────┘  │
│                                                  │
│  ┌───────────────────────────────────────────┐  │
│  │  MessageTypes.cs (Constants)             │  │
│  │  - Standardized message types             │  │
│  │  - Event data structures                  │  │
│  └───────────────────────────────────────────┘  │
└──────────────────────────────────────────────────┘
```

## Files

- **WebGLBridge.cs** - Main GameObject component that handles all communication
- **JSONBridge.cs** - JSON serialization utilities and data structures
- **BufferBridge.cs** - Binary and array data transfer utilities
- **MessageTypes.cs** - Standardized message type constants and event structures

## Setup

### 1. Unity Setup

1. Create a GameObject in your scene named **"WebGLBridge"**
2. Attach the `WebGLBridge.cs` script to it
3. The bridge will automatically set itself as a singleton and persist across scenes

```csharp
// The WebGLBridge is automatically initialized
// Access it via: WebGLBridge.Instance
```

### 2. JavaScript/React Setup

The React side is already configured in `/website/astro/src/components/unity-react/`:

- `ReactUnity.tsx` - React component
- `unityService.ts` - Service for communication
- `typeUnity.ts` - TypeScript types

## Usage Examples

### 1. Sending JSON Data to JavaScript

#### From Unity:

```csharp
using BugWars.JavaScriptBridge;

// Simple message
WebGLBridge.SendToWeb("GameReady", new {
    timestamp = DateTime.UtcNow.ToString("o")
});

// Player data
var playerData = new PlayerData {
    playerId = "player-123",
    playerName = "BugWarrior",
    level = 5,
    health = 80,
    maxHealth = 100
};
WebGLBridge.SendToWeb("PlayerSpawned", playerData);

// Using message types
MessageExtensions.SendPlayerSpawned(
    "player-123",
    "BugWarrior",
    transform.position,
    80,
    100
);
```

#### Receiving in JavaScript:

```typescript
// In React component or unityService.ts
addEventListener('GameReady', (data) => {
  console.log('Game ready:', data);
});

addEventListener('PlayerSpawned', (playerData) => {
  console.log('Player spawned:', playerData);
  // playerData = { playerId, playerName, level, health, maxHealth }
});
```

### 2. Receiving JSON Data from JavaScript

#### From JavaScript:

```typescript
// In React/TypeScript
import { unityService } from './unityService';

// Send session data
unityService.sendToUnity({
  gameObject: 'WebGLBridge',
  method: 'OnSessionUpdate',
  parameter: JSON.stringify({
    userId: 'user-123',
    email: 'user@example.com'
  })
});

// Send custom message
unityService.sendToUnity({
  gameObject: 'WebGLBridge',
  method: 'OnMessage',
  parameter: JSON.stringify({
    type: 'TeleportPlayer',
    payload: JSON.stringify({ x: 10, y: 0, z: 20 })
  })
});
```

#### Receiving in Unity:

```csharp
using BugWars.JavaScriptBridge;
using VContainer;

public class GameController : MonoBehaviour
{
    [Inject] private EventManager _eventManager;

    void Start()
    {
        // Listen for session updates
        _eventManager.AddListener<SessionData>("SessionUpdated", OnSessionUpdated);

        // Listen for custom messages
        _eventManager.AddListener<string>("Web_TeleportPlayer", OnTeleportPlayer);
    }

    private void OnSessionUpdated(SessionData data)
    {
        Debug.Log($"User logged in: {data.userId}");
    }

    private void OnTeleportPlayer(string payload)
    {
        var pos = JsonUtility.FromJson<Vector3Data>(payload);
        // Teleport player to position
    }
}
```

### 3. Sending Array/Buffer Data to JavaScript

#### Float Arrays (Terrain Heightmaps):

```csharp
using BugWars.JavaScriptBridge;

// Send terrain heightmap
float[,] heightMap = GenerateTerrainHeightmap();
BufferBridge.SendTerrainHeightmap("TerrainData", chunkX, chunkZ, heightMap);
```

#### Mesh Data:

```csharp
using BugWars.JavaScriptBridge;

// Send mesh data (vertices, triangles, normals, UVs)
Mesh mesh = GetComponent<MeshFilter>().mesh;
BufferBridge.SendMeshData("MeshGenerated", mesh);
```

#### Binary Data:

```csharp
using BugWars.JavaScriptBridge;

// Send binary data (e.g., compressed data, images)
byte[] binaryData = GetCompressedGameData();
BufferBridge.SendByteArray("CompressedData", binaryData);
```

#### Custom Arrays:

```csharp
using BugWars.JavaScriptBridge;

// Send float array (e.g., audio samples, particle positions)
float[] audioSamples = new float[1024];
BufferBridge.SendFloatArray("AudioData", audioSamples);

// Send int array (e.g., entity IDs, indices)
int[] entityIds = GetAllEntityIds();
BufferBridge.SendIntArray("EntityList", entityIds);
```

### 4. Receiving Array/Buffer Data from JavaScript

#### From JavaScript:

```typescript
// Send float array as base64
const floatArray = new Float32Array([1.0, 2.0, 3.0, 4.0]);
const base64 = btoa(String.fromCharCode(...new Uint8Array(floatArray.buffer)));

sendMessage('WebGLBridge', 'OnBinaryData', base64);

// Or send as JSON array
sendMessage('WebGLBridge', 'OnArrayData', JSON.stringify({
  data: Array.from(floatArray)
}));
```

#### Receiving in Unity:

```csharp
using BugWars.JavaScriptBridge;

// The WebGLBridge automatically handles these
// Listen for the events via EventManager

_eventManager.AddListener<byte[]>("BinaryDataReceived", OnBinaryDataReceived);
_eventManager.AddListener<float[]>("ArrayDataReceived", OnArrayDataReceived);

private void OnBinaryDataReceived(byte[] data)
{
    Debug.Log($"Received {data.Length} bytes");
    // Process binary data
}

private void OnArrayDataReceived(float[] data)
{
    Debug.Log($"Received {data.Length} floats");
    // Process array data
}
```

### 5. Data Persistence with Supabase

#### Save Data from Unity:

```csharp
using BugWars.JavaScriptBridge;

// Save player data
var playerData = new PlayerData {
    playerId = userId,
    playerName = "BugWarrior",
    level = 5,
    experience = 1250,
    health = 80,
    maxHealth = 100
};

WebGLBridge.Instance.SaveData("player_data", playerData);
```

#### Load Data from Unity:

```csharp
using BugWars.JavaScriptBridge;

// Request data load
WebGLBridge.Instance.RequestData("player_data", $"userId={userId}");

// Listen for loaded data
_eventManager.AddListener<string>("DataLoaded", OnDataLoaded);

private void OnDataLoaded(string jsonData)
{
    var playerData = JsonUtility.FromJson<PlayerData>(jsonData);
    // Apply loaded data to game
}
```

## Advanced Examples

### Example 1: Sending Complete Player State

```csharp
using BugWars.JavaScriptBridge;

public class PlayerSync : MonoBehaviour
{
    public void SyncPlayerToWeb()
    {
        var player = GetComponent<Player>();

        var playerData = new PlayerData {
            playerId = player.Id,
            playerName = player.Name,
            level = player.Level,
            experience = player.Experience,
            health = player.Health,
            maxHealth = player.MaxHealth,
            position = new Vector3Data {
                x = transform.position.x,
                y = transform.position.y,
                z = transform.position.z
            },
            inventory = player.Inventory.GetItemIds(),
            stats = player.Stats.ToDictionary()
        };

        WebGLBridge.SendToWeb("PlayerSynced", playerData);
    }
}
```

### Example 2: Sending Procedural Terrain Chunk

```csharp
using BugWars.JavaScriptBridge;

public class TerrainChunkSync : MonoBehaviour
{
    public void SendChunkToWeb(TerrainChunk chunk)
    {
        // Send metadata
        var metadata = new ChunkEvent {
            chunkX = chunk.ChunkX,
            chunkZ = chunk.ChunkZ,
            isLoaded = true,
            vertexCount = chunk.Mesh.vertexCount,
            generationTime = chunk.GenerationTime
        };

        WebGLBridge.SendToWeb("ChunkMetadata", metadata);

        // Send heightmap as float array
        BufferBridge.SendTerrainHeightmap(
            "ChunkHeightmap",
            chunk.ChunkX,
            chunk.ChunkZ,
            chunk.HeightMap
        );

        // Send mesh data
        BufferBridge.SendMeshData("ChunkMesh", chunk.Mesh);
    }
}
```

### Example 3: Real-time Entity Synchronization

```csharp
using BugWars.JavaScriptBridge;
using System.Collections.Generic;

public class EntitySyncManager : MonoBehaviour
{
    private float syncInterval = 0.1f; // 10 times per second
    private float lastSyncTime;

    void Update()
    {
        if (Time.time - lastSyncTime >= syncInterval)
        {
            SyncAllEntities();
            lastSyncTime = Time.time;
        }
    }

    private void SyncAllEntities()
    {
        var entities = EntityManager.Instance.GetAllActiveEntities();
        var entityData = new List<EntityEvent>();

        foreach (var entity in entities)
        {
            entityData.Add(new EntityEvent {
                entityId = entity.Id,
                entityType = entity.Type,
                position = new Vector3Data {
                    x = entity.transform.position.x,
                    y = entity.transform.position.y,
                    z = entity.transform.position.z
                },
                rotation = new QuaternionData {
                    x = entity.transform.rotation.x,
                    y = entity.transform.rotation.y,
                    z = entity.transform.rotation.z,
                    w = entity.transform.rotation.w
                },
                health = entity.Health,
                isActive = entity.IsActive,
                state = entity.CurrentState
            });
        }

        // Send as JSON array
        var wrapper = new { entities = entityData };
        WebGLBridge.SendToWeb("EntitiesSync", wrapper);
    }
}
```

## JavaScript/TypeScript Integration

### Receiving Messages in React:

```typescript
import { useEffect } from 'react';
import { unityService } from './unityService';

export function GameDashboard() {
  useEffect(() => {
    // Listen for player events
    const unsubscribePlayer = unityService.on(
      UnityEventType.PLAYER_SPAWNED,
      (event) => {
        console.log('Player spawned:', event.data);
        // Update UI with player data
      }
    );

    // Listen for chunk events
    const unsubscribeChunk = unityService.addEventListener(
      'ChunkLoaded',
      (data) => {
        console.log('Chunk loaded:', data);
        // Update minimap or terrain visualization
      }
    );

    return () => {
      unsubscribePlayer();
      unsubscribeChunk();
    };
  }, []);

  return <div>Game Dashboard</div>;
}
```

### Handling Binary Data in JavaScript:

```javascript
// Receive Float32Array
addEventListener('TerrainData_Heightmap', (base64OrArray) => {
  // If sent via BufferBridge.SendFloatArray, it arrives as Float32Array
  // If sent via WebGLBridge with base64, decode it:
  const buffer = Uint8Array.from(atob(base64OrArray), c => c.charCodeAt(0));
  const floatArray = new Float32Array(buffer.buffer);

  console.log('Heightmap received:', floatArray.length, 'points');
  // Visualize terrain data
});

// Receive mesh data
addEventListener('MeshGenerated_Vertices', (vertices) => {
  console.log('Mesh vertices:', vertices);
  // Create Three.js or WebGL visualization
});
```

## Best Practices

1. **Message Naming**: Use consistent naming conventions from `MessageTypes.cs`
2. **Data Size**: For large data (>1MB), use `BufferBridge` instead of JSON
3. **Performance**: Batch updates when possible (e.g., send all entities at once)
4. **Error Handling**: Always wrap Unity -> JS calls in try-catch
5. **Type Safety**: Use the provided data structures for consistency
6. **Testing**: Test in both Unity Editor (logs) and WebGL build

## Limitations

- **Unity Editor**: DllImport functions only work in WebGL build, not in editor
  - Editor logs show "(Editor Mode)" messages instead
- **JSON Arrays**: Unity's JsonUtility doesn't support top-level arrays
  - Use wrapper objects: `{ items: [...] }`
- **Binary Data**: Large binary transfers are base64 encoded (33% overhead)
  - Use typed arrays when possible for better performance

## Debugging

### Unity Side:

```csharp
// Enable detailed logging
Debug.Log("[WebGLBridge] Current state: " + JsonUtility.ToJson(state));
```

### JavaScript Side:

```typescript
// Log all Unity events
Object.values(UnityEventType).forEach(eventType => {
  addEventListener(eventType, (data) => {
    console.log(`[Unity] ${eventType}:`, data);
  });
});
```

## Integration with Existing Systems

### With VContainer:

```csharp
// WebGLBridge already supports VContainer injection
[Inject] private EventManager _eventManager;

// Events are automatically routed through EventManager
_eventManager.TriggerEvent("SessionUpdated", sessionData);
```

### With EntityManager:

```csharp
// Listen for entity events from web
_eventManager.AddListener<EntityEvent>("Web_SpawnEntity", OnSpawnEntityFromWeb);

private void OnSpawnEntityFromWeb(EntityEvent data)
{
    var entity = EntityManager.Instance.CreateEntity(data.entityType);
    entity.transform.position = new Vector3(
        data.position.x,
        data.position.y,
        data.position.z
    );
}
```

## Next Steps

1. **Add JavaScript Plugin**: Create `.jslib` file for custom JavaScript functions
2. **WebGL Templates**: Customize WebGL template to add helper functions
3. **Compression**: Add data compression for large transfers
4. **WebRTC**: Consider WebRTC for real-time multiplayer data

## Support

For questions or issues:
- Check Unity console for error messages
- Check browser console for JavaScript errors
- Verify GameObject name is exactly "WebGLBridge"
- Ensure react-unity-webgl is properly configured
