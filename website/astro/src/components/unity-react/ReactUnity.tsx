/**
 * ReactUnity Component
 * Main React component for Unity WebGL integration
 * Uses react-unity-webgl library
 */

import { useEffect, useState, useCallback } from 'react';
import type { FC } from 'react';
import { Unity, useUnityContext } from 'react-unity-webgl';
import { unityService } from './unityService';
import { useSession } from '@/components/providers/SupaProvider';
import type {
  UnityConfig,
  UnityEvent
} from './typeUnity';
import { UnityEventType } from './typeUnity';

/**
 * ReactUnity Component Props
 */
export interface ReactUnityProps {
  /**
   * Unity build configuration
   */
  config: UnityConfig;

  /**
   * CSS class name for the container
   */
  className?: string;

  /**
   * Canvas ID (default: 'unity-canvas')
   */
  canvasId?: string;

  /**
   * Whether to show fullscreen button (default: true)
   */
  showFullscreenButton?: boolean;

  /**
   * Whether to start in fullscreen mode (default: false)
   */
  startFullscreen?: boolean;

  /**
   * Custom loading component
   */
  loadingComponent?: React.ReactNode;

  /**
   * Callback when Unity is ready
   */
  onReady?: () => void;

  /**
   * Callback for Unity events
   */
  onUnityEvent?: (event: UnityEvent) => void;

  /**
   * Callback for errors
   */
  onError?: (error: string) => void;

  /**
   * Device pixel ratio (default: window.devicePixelRatio)
   */
  devicePixelRatio?: number;

  /**
   * Tab index (default: 1)
   */
  tabIndex?: number;
}

/**
 * Default Loading Component
 */
const DefaultLoading: FC<{ progress: number }> = ({ progress }) => (
  <div className="unity-loading">
    <div className="unity-loading-content">
      <div className="unity-loading-spinner"></div>
      <div className="unity-loading-bar">
        <div
          className="unity-loading-bar-fill"
          style={{ width: `${progress * 100}%` }}
        ></div>
      </div>
      <p className="unity-loading-text">Loading Unity... {Math.round(progress * 100)}%</p>
    </div>
  </div>
);

/**
 * ReactUnity Component
 */
export const ReactUnity: FC<ReactUnityProps> = ({
  config,
  className = '',
  canvasId = 'unity-canvas',
  showFullscreenButton = true,
  startFullscreen = false,
  loadingComponent,
  onReady,
  onUnityEvent,
  onError,
  devicePixelRatio,
  tabIndex = 1
}) => {
  const { session, ready: sessionReady } = useSession();
  const [isFullscreen, setIsFullscreen] = useState(false);

  // Use the react-unity-webgl hook
  const {
    unityProvider,
    isLoaded,
    loadingProgression,
    requestFullscreen,
    unload,
    sendMessage,
    addEventListener,
    removeEventListener
  } = useUnityContext(config);

  /**
   * Register Unity instance with singleton service
   */
  useEffect(() => {
    if (isLoaded) {
      unityService.setUnityContext({ sendMessage, requestFullscreen, unload });
      onReady?.();

      // Emit loaded event
      const event: UnityEvent = {
        type: UnityEventType.GAME_LOADED,
        timestamp: Date.now()
      };
      unityService.emit(event);
      onUnityEvent?.(event);

      // Request fullscreen if configured
      if (startFullscreen) {
        requestFullscreen(true);
      }
    }
  }, [isLoaded, sendMessage, requestFullscreen, unload, onReady, startFullscreen, onUnityEvent]);

  /**
   * Send session info to Unity when loaded and session is available
   */
  useEffect(() => {
    if (isLoaded && sessionReady && session) {
      sendMessage('WebGLBridge', 'OnSessionUpdate', JSON.stringify({
        userId: session.user?.id,
        email: session.user?.email
      }));
    }
  }, [isLoaded, sessionReady, session, sendMessage]);

  /**
   * Listen for custom Unity events
   */
  useEffect(() => {
    const handleCustomEvent = (eventType: string, data: string) => {
      try {
        const parsedData = JSON.parse(data);
        const event: UnityEvent = {
          type: eventType,
          data: parsedData,
          timestamp: Date.now()
        };
        unityService.emit(event);
        onUnityEvent?.(event);
      } catch (error) {
        console.error('Error parsing Unity event data:', error);
      }
    };

    // Listen for common Unity events
    addEventListener('GameReady', () => handleCustomEvent(UnityEventType.GAME_READY, '{}'));
    addEventListener('PlayerSpawned', (data) => handleCustomEvent(UnityEventType.PLAYER_SPAWNED, data));
    addEventListener('GameOver', (data) => handleCustomEvent(UnityEventType.GAME_OVER, data));
    addEventListener('ScoreUpdated', (data) => handleCustomEvent(UnityEventType.SCORE_UPDATED, data));
    addEventListener('LevelCompleted', (data) => handleCustomEvent(UnityEventType.LEVEL_COMPLETED, data));
    addEventListener('CustomEvent', (data) => handleCustomEvent(UnityEventType.CUSTOM, data));

    return () => {
      removeEventListener('GameReady', () => {});
      removeEventListener('PlayerSpawned', () => {});
      removeEventListener('GameOver', () => {});
      removeEventListener('ScoreUpdated', () => {});
      removeEventListener('LevelCompleted', () => {});
      removeEventListener('CustomEvent', () => {});
    };
  }, [addEventListener, removeEventListener, onUnityEvent]);

  /**
   * Toggle fullscreen
   */
  const handleFullscreenToggle = useCallback(() => {
    requestFullscreen(!isFullscreen);
    setIsFullscreen(!isFullscreen);
  }, [isFullscreen, requestFullscreen]);

  /**
   * Handle fullscreen change events
   */
  useEffect(() => {
    const handleFullscreenChange = () => {
      const isCurrentlyFullscreen = !!(
        document.fullscreenElement ||
        (document as any).webkitFullscreenElement ||
        (document as any).mozFullScreenElement ||
        (document as any).msFullscreenElement
      );
      setIsFullscreen(isCurrentlyFullscreen);
    };

    document.addEventListener('fullscreenchange', handleFullscreenChange);
    document.addEventListener('webkitfullscreenchange', handleFullscreenChange);
    document.addEventListener('mozfullscreenchange', handleFullscreenChange);
    document.addEventListener('msfullscreenchange', handleFullscreenChange);

    return () => {
      document.removeEventListener('fullscreenchange', handleFullscreenChange);
      document.removeEventListener('webkitfullscreenchange', handleFullscreenChange);
      document.removeEventListener('mozfullscreenchange', handleFullscreenChange);
      document.removeEventListener('msfullscreenchange', handleFullscreenChange);
    };
  }, []);

  return (
    <div
      className={`unity-container ${className}`}
      style={{
        position: 'relative',
        width: '100%',
        height: '100%',
        overflow: 'hidden'
      }}
    >
      {/* Unity Component from react-unity-webgl */}
      <Unity
        unityProvider={unityProvider}
        id={canvasId}
        className="unity-canvas"
        style={{
          width: '100%',
          height: '100%',
          visibility: isLoaded ? 'visible' : 'hidden',
          background: '#000'
        }}
        devicePixelRatio={devicePixelRatio}
        tabIndex={tabIndex}
      />

      {/* Loading Overlay */}
      {!isLoaded && (
        <div
          className="unity-loading-overlay"
          style={{
            position: 'absolute',
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            background: '#000',
            zIndex: 10
          }}
        >
          {loadingComponent || <DefaultLoading progress={loadingProgression} />}
        </div>
      )}

      {/* Fullscreen Button */}
      {showFullscreenButton && isLoaded && (
        <button
          onClick={handleFullscreenToggle}
          className="unity-fullscreen-button"
          style={{
            position: 'absolute',
            top: '1rem',
            right: '1rem',
            padding: '0.5rem 1rem',
            background: 'rgba(0, 0, 0, 0.7)',
            color: 'white',
            border: '1px solid rgba(255, 255, 255, 0.3)',
            borderRadius: '4px',
            cursor: 'pointer',
            zIndex: 20,
            fontSize: '0.875rem',
            transition: 'background 0.2s'
          }}
          onMouseEnter={(e) => {
            e.currentTarget.style.background = 'rgba(0, 0, 0, 0.9)';
          }}
          onMouseLeave={(e) => {
            e.currentTarget.style.background = 'rgba(0, 0, 0, 0.7)';
          }}
        >
          {isFullscreen ? '⛶ Exit Fullscreen' : '⛶ Fullscreen'}
        </button>
      )}
    </div>
  );
};

export default ReactUnity;
