import { useState, useMemo } from 'react';
import type { ContentBlock } from '../../types';
import { LightboxCarousel, type LightboxImage } from '../ImageLightbox';
import { rewriteImageUrl } from '../../utils/imageUrl';

interface Props {
  block: ContentBlock;
}

/**
 * Renders a responsive grid of image thumbnails sharing a single
 * lightbox carousel - click any image to open the lightbox at that
 * index, then use arrow keys or the on-screen prev/next buttons to
 * walk through the gallery without closing.
 *
 * If a gallery image has `timestampUrl` (e.g. a YouTube ?t=170 link),
 * clicking the thumbnail navigates there instead of opening the lightbox
 * - useful for video-frame galleries where the user usually wants to
 * jump back to that moment in the video.
 */
export function GalleryBlock({ block }: Props) {
  const images = block.images ?? [];

  const lightboxImages: LightboxImage[] = useMemo(
    () => images.map((img) => ({
      src: rewriteImageUrl(img.url),
      alt: img.alt,
      caption: img.caption,
    })),
    [images],
  );

  const [lightboxIndex, setLightboxIndex] = useState<number | null>(null);

  if (images.length === 0) {
    return (
      <div className="block-gallery block-gallery-empty">
        <div className="block-gallery-empty-msg">Gallery block has no images.</div>
      </div>
    );
  }

  const openLightbox = (i: number) => {
    const img = images[i];
    if (img.timestampUrl) {
      window.open(img.timestampUrl, '_blank', 'noopener,noreferrer');
      return;
    }
    setLightboxIndex(i);
  };

  return (
    <div className="block-gallery">
      {block.label && <h4 className="block-label">{block.label}</h4>}
      <div className="block-gallery-grid">
        {images.map((img, i) => {
          const thumbSrc = rewriteImageUrl(img.thumbnail ?? img.url);
          return (
            <button
              key={i}
              type="button"
              className="block-gallery-item"
              onClick={() => openLightbox(i)}
              title={img.caption ?? img.alt ?? `Image ${i + 1}`}
              aria-label={img.alt ?? `Open image ${i + 1}`}
            >
              <img src={thumbSrc} alt={img.alt ?? ''} loading="lazy" />
              {img.caption && <div className="block-gallery-item-caption">{img.caption}</div>}
            </button>
          );
        })}
      </div>
      {block.caption && <div className="block-gallery-caption">{block.caption}</div>}
      <LightboxCarousel
        images={lightboxImages}
        index={lightboxIndex ?? 0}
        visible={lightboxIndex !== null}
        onClose={() => setLightboxIndex(null)}
        onIndexChange={(i) => setLightboxIndex(i)}
      />
    </div>
  );
}
