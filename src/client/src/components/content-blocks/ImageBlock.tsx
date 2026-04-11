import { useState } from 'react';
import { Image, Maximize2, X } from 'lucide-react';
import type { ContentBlock } from '../../types';

interface Props {
  block: ContentBlock;
}

export function ImageBlock({ block }: Props) {
  const [expanded, setExpanded] = useState(false);
  const src = block.src ?? (typeof block.body === 'string' ? block.body : '');
  const alt = block.alt ?? block.label ?? 'Image';

  if (!src) {
    return (
      <div className="block-image-empty">
        <Image size={24} />
        <span>No image source provided</span>
      </div>
    );
  }

  return (
    <div className="block-image">
      {block.label && <h4 className="block-label">{block.label}</h4>}
      <div className="image-container">
        <img
          src={src}
          alt={alt}
          style={block.width ? { maxWidth: `${block.width}px` } : undefined}
          className="image-content"
          onClick={() => setExpanded(true)}
        />
        <button
          className="image-expand-btn"
          onClick={() => setExpanded(true)}
          title="Expand image"
        >
          <Maximize2 size={14} />
        </button>
      </div>
      {expanded && (
        <div className="image-overlay" onClick={() => setExpanded(false)}>
          <button className="image-overlay-close" onClick={() => setExpanded(false)}>
            <X size={20} />
          </button>
          <img src={src} alt={alt} className="image-overlay-img" onClick={(e) => e.stopPropagation()} />
        </div>
      )}
    </div>
  );
}
