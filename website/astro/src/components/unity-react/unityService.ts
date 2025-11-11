/**
 * Unity Service - Singleton
 * Manages Unity WebGL communication with Supabase and event handling
 * Works with react-unity-webgl library
 */

import type {
  UnityEvent,
  WebToUnityMessage,
  SupabasePayload,
  PlayerData,
  GameState
} from './typeUnity';
import { UnityEventType } from './typeUnity';
import { getSupa } from '@/lib/supa';

/**
 * Unity Context interface from react-unity-webgl
 */
interface UnityContext {
  sendMessage: (gameObjectName: string, methodName: string, parameter?: string | number) => void;
  requestFullscreen: (fullscreen: boolean) => void;
  unload: () => Promise<void>;
}

/**
 * UnityService - Singleton class for managing Unity WebGL integration
 */
class UnityService {
  private static instance: UnityService | null = null;
  private unityContext: UnityContext | null = null;
  private eventHandlers: Map<string, Set<(event: UnityEvent) => void>> = new Map();

  // Private constructor to enforce singleton pattern
  private constructor() {
    // Reserved for future initialization
  }

  /**
   * Get singleton instance
   */
  public static getInstance(): UnityService {
    if (!UnityService.instance) {
      UnityService.instance = new UnityService();
    }
    return UnityService.instance;
  }

  /**
   * Set Unity context from react-unity-webgl hook
   */
  public setUnityContext(context: UnityContext): void {
    this.unityContext = context;
  }


  /**
   * Send message to Unity
   */
  public sendToUnity(message: WebToUnityMessage): void {
    if (!this.unityContext) {
      console.warn('Unity context not set. Make sure Unity is loaded.');
      return;
    }

    try {
      this.unityContext.sendMessage(
        message.gameObject,
        message.method,
        message.parameter
      );
    } catch (error) {
      console.error('Error sending message to Unity:', error);
      this.emit({
        type: UnityEventType.ERROR,
        data: error instanceof Error ? error : new Error(String(error)),
        timestamp: Date.now()
      });
    }
  }

  /**
   * Subscribe to Unity events
   */
  public on(eventType: string | UnityEventType, handler: (event: UnityEvent) => void): () => void {
    if (!this.eventHandlers.has(eventType)) {
      this.eventHandlers.set(eventType, new Set());
    }
    this.eventHandlers.get(eventType)!.add(handler);

    // Return unsubscribe function
    return () => {
      const handlers = this.eventHandlers.get(eventType);
      if (handlers) {
        handlers.delete(handler);
        if (handlers.size === 0) {
          this.eventHandlers.delete(eventType);
        }
      }
    };
  }

  /**
   * Emit event to all subscribers
   */
  public emit(event: UnityEvent): void {
    // Emit to specific event type handlers
    const handlers = this.eventHandlers.get(event.type);
    if (handlers) {
      handlers.forEach(handler => {
        try {
          handler(event);
        } catch (error) {
          console.error(`Error in event handler for ${event.type}:`, error);
        }
      });
    }

    // Emit to wildcard handlers
    const wildcardHandlers = this.eventHandlers.get('*');
    if (wildcardHandlers) {
      wildcardHandlers.forEach(handler => {
        try {
          handler(event);
        } catch (error) {
          console.error('Error in wildcard event handler:', error);
        }
      });
    }
  }

  /**
   * Save data to Supabase
   */
  public async saveToSupabase(payload: SupabasePayload): Promise<any> {
    try {
      const supa = getSupa();
      const { data, error } = await supa.client
        .from(payload.table)
        .upsert(payload.data);

      if (error) {
        throw error;
      }

      // Notify Unity that save was successful
      this.sendToUnity({
        gameObject: 'WebGLBridge',
        method: 'OnDataSaved',
        parameter: JSON.stringify({ success: true, data })
      });

      return data;
    } catch (error) {
      console.error('Error saving to Supabase:', error);

      // Notify Unity that save failed
      this.sendToUnity({
        gameObject: 'WebGLBridge',
        method: 'OnDataSaved',
        parameter: JSON.stringify({ success: false, error: String(error) })
      });

      throw error;
    }
  }

  /**
   * Load data from Supabase
   */
  public async loadFromSupabase(table: string, filters?: Record<string, any>): Promise<any> {
    try {
      const supa = getSupa();
      let query = supa.client.from(table).select('*');

      // Apply filters if provided
      if (filters) {
        Object.entries(filters).forEach(([key, value]) => {
          query = query.eq(key, value);
        });
      }

      const { data, error } = await query;

      if (error) {
        throw error;
      }

      // Notify Unity with loaded data
      this.sendToUnity({
        gameObject: 'WebGLBridge',
        method: 'OnDataLoaded',
        parameter: JSON.stringify({ success: true, data })
      });

      return data;
    } catch (error) {
      console.error('Error loading from Supabase:', error);

      // Notify Unity that load failed
      this.sendToUnity({
        gameObject: 'WebGLBridge',
        method: 'OnDataLoaded',
        parameter: JSON.stringify({ success: false, error: String(error) })
      });

      throw error;
    }
  }

  /**
   * Save player data
   */
  public async savePlayerData(playerData: PlayerData): Promise<void> {
    await this.saveToSupabase({
      action: 'save',
      table: 'player_data',
      data: playerData,
      userId: playerData.id
    });
  }

  /**
   * Save game state
   */
  public async saveGameState(gameState: GameState, userId: string): Promise<void> {
    await this.saveToSupabase({
      action: 'save',
      table: 'game_states',
      data: { ...gameState, user_id: userId, updated_at: new Date().toISOString() },
      userId
    });
  }

  /**
   * Set fullscreen mode
   */
  public setFullscreen(fullscreen: boolean): void {
    if (this.unityContext) {
      this.unityContext.requestFullscreen(fullscreen);
    }
  }

  /**
   * Unload Unity instance
   */
  public async unload(): Promise<void> {
    if (this.unityContext) {
      await this.unityContext.unload();
      this.unityContext = null;
    }
  }

  /**
   * Check if Unity context is set
   */
  public isReady(): boolean {
    return this.unityContext !== null;
  }
}

// Export singleton instance
export const unityService = UnityService.getInstance();
