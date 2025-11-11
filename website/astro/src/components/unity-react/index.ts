/**
 * Unity React Integration - Barrel Exports
 * Centralized exports for Unity WebGL integration
 */

// Export main React component
export { ReactUnity, type ReactUnityProps } from './ReactUnity';

// Export Unity Service
export { unityService } from './unityService';

// Export all types
export type {
  UnityInstance,
  UnityConfig,
  UnityProgress,
  UnityEvent,
  UnityServiceEventHandlers,
  UnityContainerProps,
  PlayerData,
  GameState,
  SupabasePayload,
  WebToUnityMessage,
  UnityToWebMessage
} from './typeUnity';

// Export enums
export { UnityEventType } from './typeUnity';
