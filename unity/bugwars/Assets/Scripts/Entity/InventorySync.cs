using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using BugWars.Network;

namespace BugWars.Entity
{
    /// <summary>
    /// Syncs player inventory with the server.
    /// Uses immediate sync strategy for inventory changes (critical for gameplay).
    /// Ensures inventory transactions are reflected on server immediately.
    /// </summary>
    [NetworkSyncable("inventory", Strategy = SyncStrategy.Immediate)]
    public class InventorySync : INetworkSyncable
    {
        private readonly EntityInventory _inventory;
        private bool _isDirty = false;

        public string SyncId => "inventory";
        public bool IsDirty => _isDirty;

        public InventorySync(EntityInventory inventory)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));

            // Subscribe to inventory change events
            _inventory.OnItemAdded += OnInventoryChanged;
            _inventory.OnItemRemoved += OnInventoryChanged;
            _inventory.OnInventoryChanged += OnInventoryListChanged;

            Debug.Log("[InventorySync] Initialized with event subscriptions");
        }

        #region INetworkSyncable Implementation

        public string SerializeForSync()
        {
            var inventoryData = new InventorySyncMessage
            {
                items = _inventory.GetAllItems().ConvertAll(item => new InventoryItemData
                {
                    item_id = item.ItemId,
                    quantity = item.Quantity,
                    metadata = item.Metadata
                }),
                max_slots = (uint)_inventory.MaxSlots
            };

            string json = JsonConvert.SerializeObject(inventoryData);
            Debug.Log($"[InventorySync] Serialized: {_inventory.ItemCount} items");
            return json;
        }

        public void DeserializeFromSync(string json)
        {
            try
            {
                var inventoryData = JsonConvert.DeserializeObject<InventorySyncMessage>(json);

                // Clear current inventory
                _inventory.ClearInventory();

                // Add items from server (notifyServer = false to avoid circular sync)
                foreach (var itemData in inventoryData.items)
                {
                    _inventory.AddItem(itemData.item_id, itemData.quantity, notifyServer: false);
                }

                Debug.Log($"[InventorySync] Deserialized from server: {inventoryData.items.Count} items");
            }
            catch (Exception e)
            {
                Debug.LogError($"[InventorySync] Failed to deserialize: {e.Message}");
            }
        }

        public async UniTask OnConnected(WebSocketManager wsManager)
        {
            Debug.Log("[InventorySync] Connected to server, requesting initial inventory");

            // Request initial inventory from server
            wsManager.SendMessage("get_inventory", "");

            await UniTask.Yield();
        }

        public void OnDisconnected()
        {
            Debug.Log("[InventorySync] Disconnected from server, inventory changes will be queued");
            // On reconnect, dirty inventory will be synced
        }

        public void MarkClean()
        {
            _isDirty = false;
        }

        public void MarkDirty()
        {
            _isDirty = true;
        }

        #endregion

        #region Event Handlers

        private void OnInventoryChanged(InventoryItem item)
        {
            _isDirty = true;
            Debug.Log($"[InventorySync] Inventory changed: {item.ItemId} x{item.Quantity}, marked dirty");
        }

        private void OnInventoryListChanged(System.Collections.Generic.List<InventoryItem> items)
        {
            _isDirty = true;
            Debug.Log($"[InventorySync] Inventory list changed: {items.Count} items, marked dirty");
        }

        #endregion

        #region Cleanup

        ~InventorySync()
        {
            // Unsubscribe from events
            if (_inventory != null)
            {
                _inventory.OnItemAdded -= OnInventoryChanged;
                _inventory.OnItemRemoved -= OnInventoryChanged;
                _inventory.OnInventoryChanged -= OnInventoryListChanged;
            }
        }

        #endregion
    }

    #region Data Structures

    [Serializable]
    internal class InventorySyncMessage
    {
        [JsonProperty("items")]
        public System.Collections.Generic.List<InventoryItemData> items;

        [JsonProperty("max_slots")]
        public uint max_slots;
    }

    [Serializable]
    internal class InventoryItemData
    {
        [JsonProperty("item_id")]
        public string item_id;

        [JsonProperty("quantity")]
        public uint quantity;

        [JsonProperty("metadata")]
        public string metadata;
    }

    #endregion
}
