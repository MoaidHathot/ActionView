import { useState } from 'react';
import { ImageOff } from 'lucide-react';
import type { ContentBlock, ImageAnnotation } from '../../types';
import { ImageLightbox } from '../ImageLightbox';
import { rewriteImageUrl } from '../../utils/imageUrl';

interface Props {
  block: ContentBlock;
}

/**
 * Single image rendered as a thumbnail with click-to-enlarge.
 *
 * Capabilities:
 *   - Lightbox on click (or navigate to `timestampUrl` if set, e.g. YouTube ?t=170)
 *   - Optional overlay annotations (arrows / boxes / circles / text)
 *   - Lazy load + loading skeleton + onError fallback
 *   - Configurable `maxWidth`
 */
export function ImageBlock({ block }: Props) {
  const rawUrl = block.url ?? (typeof block.body === 'string' ? block.body : '');
  const src = rewriteImageUrl(rawUrl);
  const alt = block.alt ?? block.label ?? '';
  const caption = block.caption;
  const [lightboxOpen, setLightboxOpen] = useState(false);
  const [loaded, setLoaded] = useState(false);
  const [errored, setErrored] = useState(false);

  if (!src) {
    return (
      <div className="block-image block-image-missing">
        <div className="block-image-missing-msg">Image block has no url.</div>
      </div>
    );
  }

  const onThumbClick = () => {
    if (block.timestampUrl) {
      window.open(block.timestampUrl, '_blank', 'noopener,noreferrer');
      return;
    }
    setLightboxOpen(true);
  };

  const style = block.maxWidth ? { maxWidth: `${block.maxWidth}px` } : undefined;

  return (
    <div className="block-image">
      {block.label && <h4 className="block-label">{block.label}</h4>}
      <button
        type="button"
        className={`block-image-thumb ${errored ? 'block-image-thumb-errored' : ''}`}
        onClick={onThumbClick}
        title={block.timestampUrl ? 'Open source link' : 'Click to enlarge'}
        aria-label={alt ? `Enlarge image: ${alt}` : 'Enlarge image'}
      >
        <span className="block-image-frame">
          {!loaded && !errored && <span className="block-image-skeleton" aria-hidden="true" />}
          {errored ? (
            <span className="block-image-error">
              <ImageOff size={20} />
              <span>Image failed to load</span>
            </span>
          ) : (
            <img
              src={src}
              alt={alt}
              style={style}
              loading="lazy"
              onLoad={() => setLoaded(true)}
              onError={() => setErrored(true)}
            />
          )}
          {loaded && !errored && block.imageAnnotations && block.imageAnnotations.length > 0 && (
            <AnnotationOverlay annotations={block.imageAnnotations} />
          )}
        </span>
      </button>
      {caption && <div className="block-image-caption">{caption}</div>}
      <ImageLightbox
        src={src}
        alt={alt}
        caption={caption}
        visible={lightboxOpen}
        onClose={() => setLightboxOpen(false)}
      />
    </div>
  );
}

function AnnotationOverlay({ annotations }: { annotations: ImageAnnotation[] }) {
  return (
    <span className="image-annotations" aria-hidden="true">
      {annotations.map((a, i) => <AnnotationMark key={i} annotation={a} />)}
    </span>
  );
}

function AnnotationMark({ annotation }: { annotation: ImageAnnotation }) {
  const level = annotation.level ?? 'info';
  const baseStyle: React.CSSProperties = {
    position: 'absolute',
    left: `${annotation.x}%`,
    top: `${annotation.y}%`,
  };

  if (annotation.shape === 'text') {
    return (
      <span className={`image-annotation image-annotation-text image-annotation-${level}`} style={baseStyle}>
        {annotation.label}
      </span>
    );
  }

  if (annotation.shape === 'arrow') {
    return (
      <span
        className={`image-annotation image-annotation-arrow image-annotation-${level}`}
        style={baseStyle}
        title={annotation.label}
      >
        \u2192
        {annotation.label && <span className="image-annotation-arrow-label">{annotation.label}</span>}
      </span>
    );
  }

  // box or circle - need width/height
  const w = annotation.width ?? 10;
  const h = annotation.height ?? 10;
  return (
    <span
      className={`image-annotation image-annotation-${annotation.shape} image-annotation-${level}`}
      style={{
        ...baseStyle,
        width: `${w}%`,
        height: `${h}%`,
      }}
      title={annotation.label}
    >
      {annotation.label && (
        <span className="image-annotation-shape-label">{annotation.label}</span>
      )}
    </span>
  );
}
