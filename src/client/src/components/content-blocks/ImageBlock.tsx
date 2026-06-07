import { useState } from 'react';
import type { ContentBlock } from '../../types';
import { ImageLightbox } from '../ImageLightbox';
import { rewriteImageUrl } from '../../utils/imageUrl';

interface Props {
  block: ContentBlock;
}

/**
 * Renders an `image` content block as a clickable thumbnail with an optional
 * caption. Clicking the thumbnail opens a lightbox modal at full size.
 *
 * Source resolution:
 *   - `block.url` is the canonical field; `block.body` (string) is accepted
 *     as a fallback so authors who think of it as the "body" still work.
 *   - file:// URLs and bare Windows paths are routed through /api/files
 *     by `rewriteImageUrl`.
 *
 * Visual fields:
 *   - `block.label` is rendered as a heading above the image (like other blocks).
 *   - `block.alt` is the <img alt> and also the modal's aria-label.
 *   - `block.caption` is rendered beneath the thumbnail and in the lightbox.
 *   - `block.maxWidth` (CSS pixels) clamps the thumbnail width;
 *      defaults to a medium-thumbnail height instead so multi-image rows align.
 */
export function ImageBlock({ block }: Props) {
  const rawUrl = block.url ?? (typeof block.body === 'string' ? block.body : '');
  const src = rewriteImageUrl(rawUrl);
  const alt = block.alt ?? block.label ?? '';
  const caption = block.caption;
  const [lightboxOpen, setLightboxOpen] = useState(false);

  if (!src) {
    return (
      <div className="block-image block-image-missing">
        <div className="block-image-missing-msg">Image block has no url.</div>
      </div>
    );
  }

  // Inline style only when the author asked for a specific max-width;
  // otherwise we let App.css apply the default medium-thumbnail sizing.
  const style = block.maxWidth ? { maxWidth: `${block.maxWidth}px` } : undefined;

  return (
    <div className="block-image">
      {block.label && <h4 className="block-label">{block.label}</h4>}
      <button
        type="button"
        className="block-image-thumb"
        onClick={() => setLightboxOpen(true)}
        title="Click to enlarge"
        aria-label={alt ? `Enlarge image: ${alt}` : 'Enlarge image'}
      >
        <img src={src} alt={alt} style={style} loading="lazy" />
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
