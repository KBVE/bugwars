/**
 * Unity WebGL Types
 * Defines all TypeScript types for Unity WebGL integration
 */

/**
 * Unity Instance interface representing the loaded Unity game
 */
export interface UnityInstance {
  /**
   * Send a message to Unity
   * @param objectName - The name of the GameObject in Unity
   * @param methodName - The method name to call
   * @param value - The value to pass (optional)
   */
  SendMessage(objectName: string, methodName: string, value?: string | number): void;

  /**
   * Set the Unity instance to fullscreen mode
   * @param fullscreen - Whether to enable fullscreen
   */
  SetFullscreen(fullscreen: boolean): void;

  /**
   * Quit the Unity instance
   */
  Quit(): Promise<void>;
}

/**
 * Unity Configuration for initializing the Unity WebGL build
 */
export interface UnityConfig {
  /**
   * Path to the Unity loader file
   */
  loaderUrl: string;

  /**
   * Path to the Unity data file
   */
  dataUrl: string;

  /**
   * Path to the Unity framework file
   */
  frameworkUrl: string;

  /**
   * Path to the Unity WebAssembly file
   */
  codeUrl: string;

  /**
   * Streaming assets URL (optional)
   */
  streamingAssetsUrl?: string;

  /**
   * Company name from Unity Player Settings
   */
  companyName?: string;

  /**
   * Product name from Unity Player Settings
   */
  productName?: string;

  /**
   * Product version from Unity Player Settings
   */
  productVersion?: string;
}

/**
 * Unity Loading Progress callback
 */
export interface UnityProgress {
  /**
   * Progress value between 0 and 1
   */
  progress: number;

  /**
   * Optional message about the current loading state
   */
  message?: string;
}

/**
 * Unity Event types that can be sent from Unity to React
 */
export enum UnityEventType {
  GAME_LOADED = 'gameLoaded',
  GAME_READY = 'gameReady',
  PLAYER_SPAWNED = 'playerSpawned',
  GAME_OVER = 'gameOver',
  SCORE_UPDATED = 'scoreUpdated',
  LEVEL_COMPLETED = 'levelCompleted',
  ERROR = 'error',
  CUSTOM = 'custom'
}

/**
 * Unity Event payload structure
 */
export interface UnityEvent<T = unknown> {
  /**
   * Type of the event
   */
  type: UnityEventType | string;

  /**
   * Event data payload
   */
  data?: T;

  /**
   * Timestamp of the event
   */
  timestamp?: number;
}

/**
 * Player data structure
 */
export interface PlayerData {
  id: string;
  username: string;
  level: number;
  score: number;
  health?: number;
  position?: {
    x: number;
    y: number;
    z: number;
  };
}

/**
 * Game state structure
 */
export interface GameState {
  isPlaying: boolean;
  isPaused: boolean;
  currentLevel: number;
  score: number;
  timeElapsed: number;
}

/**
 * Supabase communication payload
 */
export interface SupabasePayload {
  action: 'save' | 'load' | 'update' | 'delete';
  table: string;
  data: Record<string, unknown>;
  userId?: string;
}

/**
 * Unity Service Event Handlers
 */
export interface UnityServiceEventHandlers {
  onGameLoaded?: () => void;
  onGameReady?: () => void;
  onProgress?: (progress: UnityProgress) => void;
  onError?: (error: Error) => void;
  onUnityEvent?: (event: UnityEvent) => void;
}

/**
 * Unity Container Props
 */
export interface UnityContainerProps {
  /**
   * Unity build configuration
   */
  config: UnityConfig;

  /**
   * CSS class name for the container
   */
  className?: string;

  /**
   * Canvas ID
   */
  canvasId?: string;

  /**
   * Event handlers
   */
  eventHandlers?: UnityServiceEventHandlers;

  /**
   * Whether to start in fullscreen mode
   */
  startFullscreen?: boolean;

  /**
   * Loading component to show while Unity loads
   */
  loadingComponent?: React.ReactNode;
}

/**
 * Message from Unity to Web
 */
export interface UnityToWebMessage {
  messageType: string;
  payload: unknown;
}

/**
 * Message from Web to Unity
 */
export interface WebToUnityMessage {
  gameObject: string;
  method: string;
  parameter?: string | number;
}
