import { useEffect } from 'react';
import { X } from 'lucide-react';

interface Props {
  src: string;
  alt?: string;
  caption?: string;
  visible: boolean;
  onClose: () => void;
}

/**
 * Full-screen modal that displays an image at its natural size, clamped to
 * the viewport. Closes on backdrop click, on the X button, or on Escape.
 *
 * The CSS is shared with the .lightbox-* class family in App.css.
 */
export function ImageLightbox({ src, alt, caption, visible, onClose }: Props) {
  // ESC to close — wired up only while the lightbox is mounted/visible so we
  // don't fight other components for the keydown.
  useEffect(() => {
    if (!visible) return;
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [visible, onClose]);

  if (!visible) return null;

  return (
    <div className="lightbox-overlay" onClick={onClose} role="dialog" aria-modal="true" aria-label={alt ?? 'Image preview'}>
      <button
        className="lightbox-close"
        onClick={onClose}
        title="Close (Esc)"
        aria-label="Close image preview"
      >
        <X size={20} />
      </button>
      <div className="lightbox-content" onClick={(e) => e.stopPropagation()}>
        <img className="lightbox-image" src={src} alt={alt ?? ''} />
        {caption && <div className="lightbox-caption">{caption}</div>}
      </div>
    </div>
  );
}
