/**
 * ReactMainHero Component
 * Main hero section with multiple background options and CTAs
 */

import { useEffect, useState, useRef, type FC } from 'react';
import type { MainHeroProps, HeroAnimationConfig } from './typeMainHero';

/**
 * ReactMainHero Component
 */
export const ReactMainHero: FC<MainHeroProps> = ({
  title,
  subtitle,
  description,
  ctaText,
  ctaUrl,
  secondaryCtaText,
  secondaryCtaUrl,
  backgroundImage,
  backgroundVideo,
  backgroundGradient,
  backgroundColor = 'transparent',
  textColor = 'white',
  height = '100vh',
  className = '',
  enableParallax = false,
  showScrollIndicator = true,
  overlayOpacity = 0.5,
  alignment = 'center',
  verticalAlignment = 'center'
}) => {
  const [scrollY, setScrollY] = useState(0);
  const [isVisible, setIsVisible] = useState(false);
  const heroRef = useRef<HTMLDivElement>(null);
  const videoRef = useRef<HTMLVideoElement>(null);

  // Handle scroll for parallax effect
  useEffect(() => {
    if (!enableParallax) return;

    const handleScroll = () => {
      setScrollY(window.scrollY);
    };

    window.addEventListener('scroll', handleScroll, { passive: true });
    return () => window.removeEventListener('scroll', handleScroll);
  }, [enableParallax]);

  // Fade in animation on mount
  useEffect(() => {
    setIsVisible(true);
  }, []);

  // Video autoplay
  useEffect(() => {
    if (videoRef.current) {
      videoRef.current.play().catch((error) => {
        console.warn('Video autoplay failed:', error);
      });
    }
  }, [backgroundVideo]);

  // Determine background style
  const getBackgroundStyle = (): React.CSSProperties => {
    const baseStyle: React.CSSProperties = {
      position: 'absolute',
      top: 0,
      left: 0,
      width: '100%',
      height: '100%',
      zIndex: -1
    };

    if (backgroundImage) {
      return {
        ...baseStyle,
        backgroundImage: `url(${backgroundImage})`,
        backgroundSize: 'cover',
        backgroundPosition: 'center',
        backgroundRepeat: 'no-repeat',
        transform: enableParallax ? `translateY(${scrollY * 0.5}px)` : undefined
      };
    }

    if (backgroundGradient) {
      return {
        ...baseStyle,
        background: backgroundGradient
      };
    }

    if (backgroundColor && backgroundColor !== 'transparent') {
      return {
        ...baseStyle,
        backgroundColor
      };
    }

    return baseStyle;
  };

  // Get content alignment styles
  const getAlignmentStyles = (): React.CSSProperties => {
    const alignmentMap = {
      left: 'flex-start',
      center: 'center',
      right: 'flex-end'
    };

    const verticalAlignmentMap = {
      top: 'flex-start',
      center: 'center',
      bottom: 'flex-end'
    };

    return {
      alignItems: alignmentMap[alignment],
      justifyContent: verticalAlignmentMap[verticalAlignment]
    };
  };

  // Get text alignment
  const getTextAlignment = (): string => {
    return alignment;
  };

  return (
    <div
      ref={heroRef}
      className={`main-hero ${className}`}
      style={{
        position: 'relative',
        width: '100%',
        height,
        overflow: 'hidden',
        display: 'flex',
        flexDirection: 'column',
        ...getAlignmentStyles()
      }}
    >
      {/* Background */}
      <div style={getBackgroundStyle()} />

      {/* Video Background */}
      {backgroundVideo && (
        <video
          ref={videoRef}
          autoPlay
          loop
          muted
          playsInline
          style={{
            position: 'absolute',
            top: '50%',
            left: '50%',
            minWidth: '100%',
            minHeight: '100%',
            width: 'auto',
            height: 'auto',
            transform: 'translate(-50%, -50%)',
            zIndex: -1,
            objectFit: 'cover'
          }}
        >
          <source src={backgroundVideo} type="video/mp4" />
        </video>
      )}

      {/* Overlay */}
      {(backgroundImage || backgroundVideo) && (
        <div
          style={{
            position: 'absolute',
            top: 0,
            left: 0,
            width: '100%',
            height: '100%',
            backgroundColor: `rgba(0, 0, 0, ${overlayOpacity})`,
            zIndex: 0
          }}
        />
      )}

      {/* Content */}
      <div
        className="hero-content"
        style={{
          position: 'relative',
          zIndex: 1,
          padding: '2rem',
          maxWidth: '1200px',
          width: '100%',
          textAlign: getTextAlignment(),
          color: textColor,
          opacity: isVisible ? 1 : 0,
          transform: isVisible ? 'translateY(0)' : 'translateY(20px)',
          transition: 'opacity 0.8s ease-out, transform 0.8s ease-out'
        }}
      >
        {/* Subtitle */}
        {subtitle && (
          <div
            className="hero-subtitle"
            style={{
              fontSize: '1.125rem',
              fontWeight: 500,
              marginBottom: '1rem',
              opacity: 0.9,
              letterSpacing: '0.05em',
              textTransform: 'uppercase'
            }}
          >
            {subtitle}
          </div>
        )}

        {/* Title */}
        <h1
          className="hero-title"
          style={{
            fontSize: 'clamp(2.5rem, 5vw, 4.5rem)',
            fontWeight: 700,
            marginBottom: '1.5rem',
            lineHeight: 1.1,
            textShadow: '0 2px 4px rgba(0, 0, 0, 0.2)'
          }}
        >
          {title}
        </h1>

        {/* Description */}
        {description && (
          <p
            className="hero-description"
            style={{
              fontSize: 'clamp(1.125rem, 2vw, 1.5rem)',
              marginBottom: '2rem',
              maxWidth: '800px',
              margin: alignment === 'center' ? '0 auto 2rem' : '0 0 2rem 0',
              opacity: 0.9,
              lineHeight: 1.6
            }}
          >
            {description}
          </p>
        )}

        {/* CTA Buttons */}
        {(ctaText || secondaryCtaText) && (
          <div
            className="hero-cta"
            style={{
              display: 'flex',
              gap: '1rem',
              flexWrap: 'wrap',
              justifyContent: alignment === 'center' ? 'center' : alignment === 'right' ? 'flex-end' : 'flex-start'
            }}
          >
            {ctaText && ctaUrl && (
              <a
                href={ctaUrl}
                className="hero-cta-primary"
                style={{
                  display: 'inline-block',
                  padding: '1rem 2rem',
                  fontSize: '1.125rem',
                  fontWeight: 600,
                  color: '#fff',
                  background: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)',
                  borderRadius: '8px',
                  textDecoration: 'none',
                  transition: 'transform 0.2s ease, box-shadow 0.2s ease',
                  boxShadow: '0 4px 6px rgba(0, 0, 0, 0.1)',
                  cursor: 'pointer'
                }}
                onMouseEnter={(e) => {
                  e.currentTarget.style.transform = 'translateY(-2px)';
                  e.currentTarget.style.boxShadow = '0 6px 12px rgba(0, 0, 0, 0.2)';
                }}
                onMouseLeave={(e) => {
                  e.currentTarget.style.transform = 'translateY(0)';
                  e.currentTarget.style.boxShadow = '0 4px 6px rgba(0, 0, 0, 0.1)';
                }}
              >
                {ctaText}
              </a>
            )}

            {secondaryCtaText && secondaryCtaUrl && (
              <a
                href={secondaryCtaUrl}
                className="hero-cta-secondary"
                style={{
                  display: 'inline-block',
                  padding: '1rem 2rem',
                  fontSize: '1.125rem',
                  fontWeight: 600,
                  color: textColor,
                  background: 'rgba(255, 255, 255, 0.1)',
                  borderRadius: '8px',
                  textDecoration: 'none',
                  transition: 'background 0.2s ease',
                  border: `2px solid ${textColor}`,
                  cursor: 'pointer'
                }}
                onMouseEnter={(e) => {
                  e.currentTarget.style.background = 'rgba(255, 255, 255, 0.2)';
                }}
                onMouseLeave={(e) => {
                  e.currentTarget.style.background = 'rgba(255, 255, 255, 0.1)';
                }}
              >
                {secondaryCtaText}
              </a>
            )}
          </div>
        )}
      </div>

      {/* Scroll Indicator */}
      {showScrollIndicator && (
        <div
          className="scroll-indicator"
          style={{
            position: 'absolute',
            bottom: '2rem',
            left: '50%',
            transform: 'translateX(-50%)',
            zIndex: 1,
            animation: 'bounce 2s infinite'
          }}
        >
          <svg
            width="24"
            height="24"
            viewBox="0 0 24 24"
            fill="none"
            stroke={textColor}
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
          >
            <polyline points="6 9 12 15 18 9" />
          </svg>
        </div>
      )}

      <style>{`
        @keyframes bounce {
          0%, 100% {
            transform: translateX(-50%) translateY(0);
          }
          50% {
            transform: translateX(-50%) translateY(10px);
          }
        }

        .main-hero {
          -webkit-font-smoothing: antialiased;
          -moz-osx-font-smoothing: grayscale;
        }

        @media (max-width: 768px) {
          .hero-content {
            padding: 1.5rem !important;
          }

          .hero-cta {
            flex-direction: column;
            align-items: stretch !important;
          }

          .hero-cta a {
            width: 100%;
            text-align: center;
          }
        }
      `}</style>
    </div>
  );
};

export default ReactMainHero;
