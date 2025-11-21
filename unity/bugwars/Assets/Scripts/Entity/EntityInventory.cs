// Assets/Scripts/Entity/EntityInventory.cs
// Entity inventory management - works for Players, NPCs, and any other entities
// Syncs with Axum server for player entities

using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace BugWars.Entity
{
    /// <summary>
    /// Represents a single inventory item (pure data structure, ECS-friendly)
    /// </summary>
    [Serializable]
    public struct InventoryItem
    {
        [JsonProperty("item_id")]
        public string ItemId;

        [JsonProperty("quantity")]
        public uint Quantity;

        [JsonProperty("metadata")]
        public string Metadata; // Optional JSON metadata

        public InventoryItem(string itemId, uint quantity, string metadata = null)
        {
            ItemId = itemId;
            Quantity = quantity;
            Metadata = metadata;
        }
    }

    /// <summary>
    /// Pure inventory data structure (JSON-ready, future ECS-friendly)
    /// Separates data from Unity-specific behavior
    /// </summary>
    [Serializable]
    public struct EntityInventoryData
    {
        [JsonProperty("max_slots")]
        public uint MaxSlots;

        [JsonProperty("items")]
        public List<InventoryItem> Items;

        public EntityInventoryData(uint maxSlots)
        {
            MaxSlots = maxSlots;
            Items = new List<InventoryItem>();
        }
    }

    /// <summary>
    /// Entity inventory MonoBehaviour - Unity-facing wrapper around EntityInventoryData
    /// Tracks items for any entity (Players, NPCs, Chests, etc.)
    /// For player entities, automatically syncs with server
    /// This is an adapter/wrapper that handles Unity-specific concerns (events, server sync)
    /// </summary>
    public class EntityInventory : MonoBehaviour
    {
        [Header("Inventory Settings")]
        [SerializeField] private bool isPlayerInventory = false; // Only players sync with server
        [SerializeField] private bool autoSyncWithServer = true;

        // Pure data structure - serialized so inspector can tweak defaults
        [SerializeField] private EntityInventoryData data = new EntityInventoryData(20);

        // WebSocket manager reference (injected via VContainer)
        private Network.WebSocketManager _wsManager;

        // VContainer injection method (optional - constructor injection preferred but field injection works for MonoBehaviours)
        [VContainer.Inject]
        public void Construct(Network.WebSocketManager wsManager)
        {
            _wsManager = wsManager;
            Debug.Log("[EntityInventory] WebSocketManager injected via VContainer");
        }

        public event Action<InventoryItem> OnItemAdded;
        public event Action<InventoryItem> OnItemRemoved;
        public event Action<List<InventoryItem>> OnInventoryChanged;

        public bool IsPlayerInventory => isPlayerInventory;
        public int ItemCount => data.Items?.Count ?? 0;
        public int MaxSlots => (int)data.MaxSlots;
        public IReadOnlyList<InventoryItem> Items => data.Items;

        #region JSON Serialization

        /// <summary>
        /// Serialize inventory data to JSON
        /// </summary>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(data);
        }

        /// <summary>
        /// Load inventory data from JSON (e.g., from server)
        /// </summary>
        public void LoadFromJson(string json)
        {
            var loaded = JsonConvert.DeserializeObject<EntityInventoryData>(json);
            data = loaded;
            data.Items ??= new List<InventoryItem>();

            OnInventoryChanged?.Invoke(data.Items);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Add an item to the inventory
        /// </summary>
        public bool AddItem(string itemId, uint quantity, bool notifyServer = true)
        {
            data.Items ??= new List<InventoryItem>();

            // Try to stack with existing item (no metadata)
            int existingIndex = FindItemIndex(itemId, requireEmptyMetadata: true);

            if (existingIndex >= 0)
            {
                var existing = data.Items[existingIndex];
                existing.Quantity += quantity;
                data.Items[existingIndex] = existing;

                Debug.Log($"[EntityInventory] Stacked {quantity}x {itemId}, new quantity: {existing.Quantity}");
                OnItemAdded?.Invoke(existing);
                OnInventoryChanged?.Invoke(data.Items);

                if (isPlayerInventory && autoSyncWithServer && notifyServer)
                    SendAddItemToServer(itemId, quantity);

                return true;
            }

            // Check slot limit
            if (data.MaxSlots > 0 && data.Items.Count >= data.MaxSlots)
            {
                Debug.LogWarning($"[EntityInventory] Inventory full! Cannot add {quantity}x {itemId}");
                return false;
            }

            // Add new item
            var newItem = new InventoryItem(itemId, quantity);
            data.Items.Add(newItem);
            Debug.Log($"[EntityInventory] Added {quantity}x {itemId} to inventory");

            OnItemAdded?.Invoke(newItem);
            OnInventoryChanged?.Invoke(data.Items);

            if (isPlayerInventory && autoSyncWithServer && notifyServer)
                SendAddItemToServer(itemId, quantity);

            return true;
        }

        /// <summary>
        /// Remove an item from the inventory
        /// </summary>
        public bool RemoveItem(string itemId, uint quantity, bool notifyServer = true)
        {
            if (data.Items == null || data.Items.Count == 0)
                return false;

            int index = FindItemIndex(itemId);
            if (index < 0)
            {
                Debug.LogWarning($"[EntityInventory] Item {itemId} not found in inventory");
                return false;
            }

            var item = data.Items[index];

            if (item.Quantity < quantity)
            {
                Debug.LogWarning($"[EntityInventory] Not enough {itemId} (have {item.Quantity}, need {quantity})");
                return false;
            }

            item.Quantity -= quantity;
            Debug.Log($"[EntityInventory] Removed {quantity}x {itemId}, remaining: {item.Quantity}");

            if (item.Quantity == 0)
            {
                data.Items.RemoveAt(index);
                Debug.Log($"[EntityInventory] {itemId} depleted, removed from inventory");
            }
            else
            {
                data.Items[index] = item;
            }

            OnItemRemoved?.Invoke(item);
            OnInventoryChanged?.Invoke(data.Items);

            if (isPlayerInventory && autoSyncWithServer && notifyServer)
                SendRemoveItemToServer(itemId, quantity);

            return true;
        }

        /// <summary>
        /// Check if entity has an item with sufficient quantity
        /// </summary>
        public bool HasItem(string itemId, uint quantity = 1)
        {
            return GetItemQuantity(itemId) >= quantity;
        }

        /// <summary>
        /// Get quantity of a specific item
        /// </summary>
        public uint GetItemQuantity(string itemId)
        {
            if (data.Items == null) return 0;
            int idx = FindItemIndex(itemId);
            return idx >= 0 ? data.Items[idx].Quantity : 0;
        }

        /// <summary>
        /// Get all items in inventory
        /// </summary>
        public List<InventoryItem> GetAllItems()
        {
            return data.Items == null ? new List<InventoryItem>() : new List<InventoryItem>(data.Items);
        }

        /// <summary>
        /// Clear the entire inventory
        /// </summary>
        public void ClearInventory()
        {
            if (data.Items == null)
                data.Items = new List<InventoryItem>();
            else
                data.Items.Clear();

            Debug.Log("[EntityInventory] Inventory cleared");
            OnInventoryChanged?.Invoke(data.Items);
        }

        /// <summary>
        /// Internal helper to find item index
        /// </summary>
        private int FindItemIndex(string itemId, bool requireEmptyMetadata = false)
        {
            if (data.Items == null) return -1;

            for (int i = 0; i < data.Items.Count; i++)
            {
                var item = data.Items[i];
                if (item.ItemId == itemId)
                {
                    if (!requireEmptyMetadata || string.IsNullOrEmpty(item.Metadata))
                        return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Set whether this is a player inventory (enables server sync)
        /// </summary>
        public void SetPlayerInventory(bool isPlayer)
        {
            isPlayerInventory = isPlayer;
        }

        /// <summary>
        /// Request inventory sync from server (player only)
        /// </summary>
        public void RequestInventoryFromServer()
        {
            if (!isPlayerInventory)
            {
                Debug.LogWarning("[EntityInventory] Cannot request from server - not a player inventory");
                return;
            }

            if (_wsManager == null || !_wsManager.IsConnected)
            {
                Debug.LogWarning("[EntityInventory] Cannot request inventory - not connected to server");
                return;
            }

            var message = new Dictionary<string, object>
            {
                { "type", "get_inventory" }
            };

            // WebSocketManager.SendMessage expects (type, data) but we need to send JSON directly
            // Use SendMessageInternal if available, or serialize manually
            string json = JsonConvert.SerializeObject(message);
            // For now, use the two-parameter version
            _wsManager.SendMessage("get_inventory", "");
            Debug.Log("[EntityInventory] Requested inventory from server");
        }

        /// <summary>
        /// Update inventory from server response (player only)
        /// </summary>
        public void UpdateFromServer(List<InventoryItem> serverItems)
        {
            data.Items = serverItems ?? new List<InventoryItem>();
            Debug.Log($"[EntityInventory] Updated from server: {data.Items.Count} items");
            OnInventoryChanged?.Invoke(data.Items);
        }

        #endregion

        #region Server Communication (Player Only)

        private void SendAddItemToServer(string itemId, uint quantity)
        {
            if (_wsManager == null || !_wsManager.IsConnected)
            {
                Debug.LogWarning("[EntityInventory] Not connected to server, cannot sync add_item");
                return;
            }

            var message = new Dictionary<string, object>
            {
                { "type", "add_item" },
                { "item_id", itemId },
                { "quantity", quantity }
            };

            // Send as properly formatted game message
            _wsManager.SendMessage("add_item", JsonConvert.SerializeObject(new { item_id = itemId, quantity }));
        }

        private void SendRemoveItemToServer(string itemId, uint quantity)
        {
            if (_wsManager == null || !_wsManager.IsConnected)
            {
                Debug.LogWarning("[EntityInventory] Not connected to server, cannot sync remove_item");
                return;
            }

            var message = new Dictionary<string, object>
            {
                { "type", "remove_item" },
                { "item_id", itemId },
                { "quantity", quantity }
            };

            // Send as properly formatted game message
            _wsManager.SendMessage("remove_item", JsonConvert.SerializeObject(new { item_id = itemId, quantity }));
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Ensure Items list is initialized
            data.Items ??= new List<InventoryItem>();

            // Note: _wsManager is injected via VContainer's Construct() method
            // If not injected (e.g., entity not created via DI), it will be null and server sync will be disabled
        }

        private void Start()
        {
            // Request initial inventory when game starts (player only)
            if (isPlayerInventory && autoSyncWithServer)
            {
                if (_wsManager != null && _wsManager.IsConnected)
                {
                    RequestInventoryFromServer();
                }
                else if (_wsManager == null)
                {
                    Debug.LogWarning("[EntityInventory] Cannot request inventory - WebSocketManager not available");
                }
            }
        }

        #endregion

        #region Debug Helpers

        [ContextMenu("Print Inventory")]
        private void PrintInventory()
        {
            Debug.Log($"=== Entity Inventory ({data.Items?.Count ?? 0}/{data.MaxSlots} slots) [Player: {isPlayerInventory}] ===");
            if (data.Items != null)
            {
                foreach (var item in data.Items)
                {
                    Debug.Log($"  {item.Quantity}x {item.ItemId}" +
                        (string.IsNullOrEmpty(item.Metadata) ? "" : $" [{item.Metadata}]"));
                }
            }
        }

        [ContextMenu("Print Inventory JSON")]
        private void PrintInventoryJson()
        {
            Debug.Log($"[EntityInventory] JSON: {ToJson()}");
        }

        [ContextMenu("Add Test Item (Sword)")]
        private void AddTestSword()
        {
            AddItem("weapon_sword", 1);
        }

        [ContextMenu("Add Test Item (Health Potion x5)")]
        private void AddTestPotion()
        {
            AddItem("consumable_health_potion", 5);
        }

        [ContextMenu("Add Test Item (Gold x100)")]
        private void AddTestGold()
        {
            AddItem("currency_gold", 100);
        }

        [ContextMenu("Request Sync from Server (Player Only)")]
        private void TestRequestSync()
        {
            RequestInventoryFromServer();
        }

        #endregion
    }
}
