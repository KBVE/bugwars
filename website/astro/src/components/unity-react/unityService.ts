/**
 * Unity Service - Singleton
 * Manages Unity WebGL instance and communication with Supabase
 */

import type {
  UnityInstance,
  UnityConfig,
  UnityEvent,
  UnityProgress,
  UnityServiceEventHandlers,
  WebToUnityMessage,
  UnityToWebMessage,
  SupabasePayload,
  PlayerData,
  GameState
} from './typeUnity';
import { UnityEventType } from './typeUnity';
import { getSupa } from '@/lib/supa';

/**
 * UnityService - Singleton class for managing Unity WebGL integration
 */
class UnityService {
  private static instance: UnityService | null = null;
  private unityInstance: UnityInstance | null = null;
  private eventHandlers: Map<string, Set<(event: UnityEvent) => void>> = new Map();
  private isInitialized = false;
  private isLoading = false;
  private loadingProgress = 0;

  // Private constructor to enforce singleton pattern
  private constructor() {
    // Set up global listener for Unity messages
    if (typeof window !== 'undefined') {
      (window as any).receiveUnityMessage = this.handleUnityMessage.bind(this);
    }
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
   * Initialize Unity WebGL instance
   */
  public async initialize(
    canvasElement: HTMLCanvasElement,
    config: UnityConfig,
    handlers?: UnityServiceEventHandlers
  ): Promise<UnityInstance> {
    if (this.isInitialized && this.unityInstance) {
      console.warn('Unity is already initialized');
      return this.unityInstance;
    }

    if (this.isLoading) {
      throw new Error('Unity is already loading');
    }

    this.isLoading = true;

    try {
      // Register event handlers if provided
      if (handlers) {
        if (handlers.onProgress) {
          this.on('progress', (event) => {
            handlers.onProgress!(event.data as UnityProgress);
          });
        }
        if (handlers.onGameLoaded) {
          this.on(UnityEventType.GAME_LOADED, handlers.onGameLoaded);
        }
        if (handlers.onGameReady) {
          this.on(UnityEventType.GAME_READY, handlers.onGameReady);
        }
        if (handlers.onError) {
          this.on(UnityEventType.ERROR, (event) => {
            handlers.onError!(event.data as Error);
          });
        }
        if (handlers.onUnityEvent) {
          this.on('*', handlers.onUnityEvent);
        }
      }

      // Load Unity loader script
      const unityLoader = await this.loadUnityLoader(config.loaderUrl);

      // Create Unity instance
      this.unityInstance = await unityLoader.createUnityInstance(canvasElement, config, (progress: number) => {
        this.loadingProgress = progress;
        this.emit({
          type: 'progress',
          data: { progress },
          timestamp: Date.now()
        });
      });

      this.isInitialized = true;
      this.isLoading = false;

      // Emit game loaded event
      this.emit({
        type: UnityEventType.GAME_LOADED,
        timestamp: Date.now()
      });

      return this.unityInstance;
    } catch (error) {
      this.isLoading = false;
      const errorObj = error instanceof Error ? error : new Error(String(error));
      this.emit({
        type: UnityEventType.ERROR,
        data: errorObj,
        timestamp: Date.now()
      });
      throw errorObj;
    }
  }

  /**
   * Load Unity loader script dynamically
   */
  private loadUnityLoader(loaderUrl: string): Promise<any> {
    return new Promise((resolve, reject) => {
      // Check if already loaded
      if ((window as any).createUnityInstance) {
        resolve({ createUnityInstance: (window as any).createUnityInstance });
        return;
      }

      const script = document.createElement('script');
      script.src = loaderUrl;
      script.async = true;
      script.onload = () => {
        if ((window as any).createUnityInstance) {
          resolve({ createUnityInstance: (window as any).createUnityInstance });
        } else {
          reject(new Error('Unity loader failed to expose createUnityInstance'));
        }
      };
      script.onerror = () => reject(new Error(`Failed to load Unity loader from ${loaderUrl}`));
      document.body.appendChild(script);
    });
  }

  /**
   * Send message to Unity
   */
  public sendToUnity(message: WebToUnityMessage): void {
    if (!this.unityInstance) {
      console.error('Unity instance not initialized');
      return;
    }

    try {
      this.unityInstance.SendMessage(
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
   * Handle messages received from Unity
   */
  private handleUnityMessage(message: string): void {
    try {
      const parsed: UnityToWebMessage = JSON.parse(message);
      const event: UnityEvent = {
        type: parsed.messageType,
        data: parsed.payload,
        timestamp: Date.now()
      };
      this.emit(event);
    } catch (error) {
      console.error('Error parsing Unity message:', error, message);
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
  private emit(event: UnityEvent): void {
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
    if (this.unityInstance) {
      this.unityInstance.SetFullscreen(fullscreen);
    }
  }

  /**
   * Quit Unity instance
   */
  public async quit(): Promise<void> {
    if (this.unityInstance) {
      await this.unityInstance.Quit();
      this.unityInstance = null;
      this.isInitialized = false;
    }
  }

  /**
   * Get current Unity instance
   */
  public getUnityInstance(): UnityInstance | null {
    return this.unityInstance;
  }

  /**
   * Check if Unity is initialized
   */
  public getIsInitialized(): boolean {
    return this.isInitialized;
  }

  /**
   * Check if Unity is loading
   */
  public getIsLoading(): boolean {
    return this.isLoading;
  }

  /**
   * Get current loading progress
   */
  public getLoadingProgress(): number {
    return this.loadingProgress;
  }
}

// Export singleton instance
export const unityService = UnityService.getInstance();
