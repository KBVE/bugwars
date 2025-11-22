using UnityEngine;
using System.Collections.Generic;
using MessagePipe;
using VContainer;
using System;
using BugWars.Interaction;

namespace BugWars.Core
{
    /// <summary>
    /// Manages player resources (wood, stone, berries, herbs)
    /// Subscribes to ResourceHarvestedMessage from HarvestAction
    /// Decoupled architecture for server-authoritative multiplayer
    /// </summary>
    public class ResourceManager : IDisposable
    {
        // Resource inventory
        private Dictionary<ResourceType, int> resources = new Dictionary<ResourceType, int>();

        // MessagePipe subscription
        private IDisposable resourceSubscription;

        [Inject]
        public ResourceManager(ISubscriber<ResourceHarvestedMessage> subscriber)
        {
            // Initialize resource counts
            resources[ResourceType.Wood] = 0;
            resources[ResourceType.Stone] = 0;
            resources[ResourceType.Berries] = 0;
            resources[ResourceType.Herbs] = 0;

            // Subscribe to harvest events
            resourceSubscription = subscriber.Subscribe(OnResourceHarvested);
        }

        /// <summary>
        /// Handle resource harvested messages
        /// </summary>
        private void OnResourceHarvested(ResourceHarvestedMessage message)
        {
            if (message.ResourceType == ResourceType.None)
                return;

            // Add resources to inventory
            if (resources.ContainsKey(message.ResourceType))
            {
                resources[message.ResourceType] += message.Amount;
            }
            else
            {
                resources[message.ResourceType] = message.Amount;
            }

            // TODO: Eventually send to server for validation in server-authoritative architecture
            // TODO: Trigger UI updates via MessagePipe (ResourceInventoryUpdatedMessage)
            // TODO: Trigger sound effects / particles
        }

        /// <summary>
        /// Get current amount of a specific resource
        /// </summary>
        public int GetResourceAmount(ResourceType resourceType)
        {
            return resources.TryGetValue(resourceType, out int amount) ? amount : 0;
        }

        /// <summary>
        /// Get all resources
        /// </summary>
        public Dictionary<ResourceType, int> GetAllResources()
        {
            return new Dictionary<ResourceType, int>(resources);
        }

        /// <summary>
        /// Add resources (for server sync or debugging)
        /// </summary>
        public void AddResources(ResourceType resourceType, int amount)
        {
            if (resources.ContainsKey(resourceType))
            {
                resources[resourceType] += amount;
            }
            else
            {
                resources[resourceType] = amount;
            }
        }

        /// <summary>
        /// Remove resources (for crafting, building, etc.)
        /// </summary>
        public bool RemoveResources(ResourceType resourceType, int amount)
        {
            if (!resources.TryGetValue(resourceType, out int current))
                return false;

            if (current < amount)
                return false;

            resources[resourceType] = current - amount;
            return true;
        }

        public void Dispose()
        {
            resourceSubscription?.Dispose();
        }
    }
}
