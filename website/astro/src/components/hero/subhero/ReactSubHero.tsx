/**
 * ReactSubHero Component
 * Smaller, secondary hero section component
 */

import { useEffect, useState, useRef, type FC } from 'react';
import type { SubHeroProps, SubHeroSizeConfig } from './typeSubHero';

/**
 * Size configurations for different variants
 */
const sizeConfigs: Record<'small' | 'medium' | 'large', SubHeroSizeConfig> = {
  small: {
    height: '30vh',
    titleSize: 'clamp(1.75rem, 3vw, 2.5rem)',
    subtitleSize: '0.875rem',
    descriptionSize: '1rem',
    padding: '2rem'
  },
  medium: {
    height: '50vh',
    titleSize: 'clamp(2rem, 4vw, 3rem)',
    subtitleSize: '1rem',
    descriptionSize: '1.125rem',
    padding: '3rem'
  },
  large: {
    height: '70vh',
    titleSize: 'clamp(2.5rem, 5vw, 3.5rem)',
    subtitleSize: '1.125rem',
    descriptionSize: '1.25rem',
    padding: '4rem'
  }
};

/**
 * ReactSubHero Component
 */
export const ReactSubHero: FC<SubHeroProps> = ({
  title,
  subtitle,
  description,
  ctaText,
  ctaUrl,
  backgroundImage,
  backgroundGradient,
  backgroundColor = '#f8f9fa',
  textColor = '#333',
  height,
  className = '',
  showDecorative = false,
  overlayOpacity = 0.3,
  alignment = 'center',
  size = 'medium',
  enableAnimation = true
}) => {
  const [isVisible, setIsVisible] = useState(!enableAnimation);
  const heroRef = useRef<HTMLDivElement>(null);

  // Get size config
  const sizeConfig = sizeConfigs[size];
  const effectiveHeight = height || sizeConfig.height;

  // Fade in animation on mount or when visible
  useEffect(() => {
    if (!enableAnimation) return;

    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            setIsVisible(true);
            observer.disconnect();
          }
        });
      },
      { threshold: 0.2 }
    );

    if (heroRef.current) {
      observer.observe(heroRef.current);
    }

    return () => observer.disconnect();
  }, [enableAnimation]);

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
        backgroundRepeat: 'no-repeat'
      };
    }

    if (backgroundGradient) {
      return {
        ...baseStyle,
        background: backgroundGradient
      };
    }

    return {
      ...baseStyle,
      backgroundColor
    };
  };

  // Get text alignment
  const getTextAlignment = (): string => {
    return alignment;
  };

  // Get content alignment styles
  const getAlignmentStyles = (): React.CSSProperties => {
    const alignmentMap = {
      left: 'flex-start',
      center: 'center',
      right: 'flex-end'
    };

    return {
      alignItems: alignmentMap[alignment]
    };
  };

  return (
    <div
      ref={heroRef}
      className={`sub-hero ${className}`}
      style={{
        position: 'relative',
        width: '100%',
        height: effectiveHeight,
        minHeight: '250px',
        overflow: 'hidden',
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'center',
        ...getAlignmentStyles()
      }}
    >
      {/* Background */}
      <div style={getBackgroundStyle()} />

      {/* Overlay */}
      {(backgroundImage || backgroundGradient) && (
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

      {/* Decorative Element */}
      {showDecorative && (
        <div
          className="decorative-element"
          style={{
            position: 'absolute',
            top: 0,
            right: 0,
            width: '200px',
            height: '200px',
            background: 'linear-gradient(135deg, rgba(102, 126, 234, 0.1) 0%, rgba(118, 75, 162, 0.1) 100%)',
            borderRadius: '50%',
            transform: 'translate(50%, -50%)',
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
          padding: sizeConfig.padding,
          maxWidth: '1200px',
          width: '100%',
          textAlign: getTextAlignment(),
          color: textColor,
          opacity: isVisible ? 1 : 0,
          transform: isVisible ? 'translateY(0)' : 'translateY(30px)',
          transition: enableAnimation ? 'opacity 0.6s ease-out, transform 0.6s ease-out' : 'none'
        }}
      >
        {/* Subtitle */}
        {subtitle && (
          <div
            className="hero-subtitle"
            style={{
              fontSize: sizeConfig.subtitleSize,
              fontWeight: 500,
              marginBottom: '0.75rem',
              opacity: 0.8,
              letterSpacing: '0.05em',
              textTransform: 'uppercase'
            }}
          >
            {subtitle}
          </div>
        )}

        {/* Title */}
        <h2
          className="hero-title"
          style={{
            fontSize: sizeConfig.titleSize,
            fontWeight: 700,
            marginBottom: '1rem',
            lineHeight: 1.2
          }}
        >
          {title}
        </h2>

        {/* Description */}
        {description && (
          <p
            className="hero-description"
            style={{
              fontSize: sizeConfig.descriptionSize,
              marginBottom: '1.5rem',
              maxWidth: '700px',
              margin: alignment === 'center' ? '0 auto 1.5rem' : '0 0 1.5rem 0',
              opacity: 0.85,
              lineHeight: 1.6
            }}
          >
            {description}
          </p>
        )}

        {/* CTA Button */}
        {ctaText && ctaUrl && (
          <div
            className="hero-cta"
            style={{
              display: 'flex',
              justifyContent: alignment === 'center' ? 'center' : alignment === 'right' ? 'flex-end' : 'flex-start'
            }}
          >
            <a
              href={ctaUrl}
              className="hero-cta-button"
              style={{
                display: 'inline-block',
                padding: '0.75rem 1.5rem',
                fontSize: '1rem',
                fontWeight: 600,
                color: '#fff',
                background: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)',
                borderRadius: '6px',
                textDecoration: 'none',
                transition: 'transform 0.2s ease, box-shadow 0.2s ease',
                boxShadow: '0 2px 4px rgba(0, 0, 0, 0.1)',
                cursor: 'pointer'
              }}
              onMouseEnter={(e) => {
                e.currentTarget.style.transform = 'translateY(-2px)';
                e.currentTarget.style.boxShadow = '0 4px 8px rgba(0, 0, 0, 0.2)';
              }}
              onMouseLeave={(e) => {
                e.currentTarget.style.transform = 'translateY(0)';
                e.currentTarget.style.boxShadow = '0 2px 4px rgba(0, 0, 0, 0.1)';
              }}
            >
              {ctaText}
            </a>
          </div>
        )}
      </div>

      <style>{`
        .sub-hero {
          -webkit-font-smoothing: antialiased;
          -moz-osx-font-smoothing: grayscale;
        }

        @media (max-width: 768px) {
          .hero-content {
            padding: 1.5rem !important;
          }

          .hero-cta-button {
            width: 100%;
            text-align: center;
          }
        }

        @media (max-width: 480px) {
          .hero-content {
            padding: 1rem !important;
          }
        }
      `}</style>
    </div>
  );
};

export default ReactSubHero;
