/**
 * ReactUnity Component
 * Main React component for Unity WebGL integration
 */

import { useEffect, useRef, useState, useCallback } from 'react';
import type { FC } from 'react';
import { unityService } from './unityService';
import { useSession } from '@/components/providers/SupaProvider';
import type {
  UnityConfig,
  UnityContainerProps,
  UnityEvent,
  UnityProgress,
  UnityInstance
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
  onReady?: (instance: UnityInstance) => void;

  /**
   * Callback for Unity events
   */
  onUnityEvent?: (event: UnityEvent) => void;

  /**
   * Callback for errors
   */
  onError?: (error: Error) => void;
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
  onError
}) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [loadingProgress, setLoadingProgress] = useState(0);
  const [error, setError] = useState<Error | null>(null);
  const [isFullscreen, setIsFullscreen] = useState(false);
  const { session, ready: sessionReady } = useSession();

  /**
   * Initialize Unity
   */
  useEffect(() => {
    if (!canvasRef.current) return;

    let isMounted = true;
    const canvas = canvasRef.current;

    const initUnity = async () => {
      try {
        setIsLoading(true);
        setError(null);

        // Subscribe to events
        const unsubscribers = [
          unityService.on('progress', (event) => {
            const progress = (event.data as UnityProgress).progress;
            if (isMounted) {
              setLoadingProgress(progress);
            }
          }),

          unityService.on(UnityEventType.GAME_LOADED, () => {
            if (isMounted) {
              setIsLoading(false);
            }
          }),

          unityService.on(UnityEventType.GAME_READY, () => {
            console.log('Unity game is ready');
          }),

          unityService.on(UnityEventType.ERROR, (event) => {
            const err = event.data as Error;
            if (isMounted) {
              setError(err);
              setIsLoading(false);
            }
            onError?.(err);
          })
        ];

        // Subscribe to all events if handler provided
        if (onUnityEvent) {
          unsubscribers.push(unityService.on('*', onUnityEvent));
        }

        // Initialize Unity instance
        const instance = await unityService.initialize(canvas, config, {
          onGameLoaded: () => {
            console.log('Unity loaded successfully');
          },
          onGameReady: () => {
            console.log('Unity ready');
            if (startFullscreen) {
              unityService.setFullscreen(true);
              setIsFullscreen(true);
            }
          },
          onError: (err) => {
            console.error('Unity error:', err);
          }
        });

        if (isMounted && onReady) {
          onReady(instance);
        }

        // Send session info to Unity if available
        if (sessionReady && session) {
          unityService.sendToUnity({
            gameObject: 'WebGLBridge',
            method: 'OnSessionUpdate',
            parameter: JSON.stringify({
              userId: session.user?.id,
              email: session.user?.email
            })
          });
        }

        // Cleanup
        return () => {
          unsubscribers.forEach(unsub => unsub());
        };
      } catch (err) {
        console.error('Failed to initialize Unity:', err);
        if (isMounted) {
          const error = err instanceof Error ? err : new Error(String(err));
          setError(error);
          setIsLoading(false);
          onError?.(error);
        }
      }
    };

    initUnity();

    return () => {
      isMounted = false;
    };
  }, [config, canvasId, startFullscreen, onReady, onUnityEvent, onError]);

  /**
   * Update Unity when session changes
   */
  useEffect(() => {
    if (sessionReady && session && !isLoading) {
      unityService.sendToUnity({
        gameObject: 'WebGLBridge',
        method: 'OnSessionUpdate',
        parameter: JSON.stringify({
          userId: session.user?.id,
          email: session.user?.email
        })
      });
    }
  }, [session, sessionReady, isLoading]);

  /**
   * Toggle fullscreen
   */
  const handleFullscreenToggle = useCallback(() => {
    const newFullscreenState = !isFullscreen;
    unityService.setFullscreen(newFullscreenState);
    setIsFullscreen(newFullscreenState);
  }, [isFullscreen]);

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
      ref={containerRef}
      className={`unity-container ${className}`}
      style={{
        position: 'relative',
        width: '100%',
        height: '100%',
        overflow: 'hidden'
      }}
    >
      {/* Canvas */}
      <canvas
        ref={canvasRef}
        id={canvasId}
        className="unity-canvas"
        style={{
          width: '100%',
          height: '100%',
          display: isLoading ? 'none' : 'block',
          background: '#000'
        }}
      />

      {/* Loading Overlay */}
      {isLoading && !error && (
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
          {loadingComponent || <DefaultLoading progress={loadingProgress} />}
        </div>
      )}

      {/* Error Display */}
      {error && (
        <div
          className="unity-error"
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
            color: '#fff',
            padding: '2rem',
            zIndex: 10
          }}
        >
          <div className="unity-error-content">
            <h2 style={{ color: '#ff4444', marginBottom: '1rem' }}>Error Loading Unity</h2>
            <p style={{ marginBottom: '1rem' }}>{error.message}</p>
            <button
              onClick={() => window.location.reload()}
              style={{
                padding: '0.5rem 1rem',
                background: '#4CAF50',
                color: 'white',
                border: 'none',
                borderRadius: '4px',
                cursor: 'pointer'
              }}
            >
              Reload Page
            </button>
          </div>
        </div>
      )}

      {/* Fullscreen Button */}
      {showFullscreenButton && !isLoading && !error && (
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
