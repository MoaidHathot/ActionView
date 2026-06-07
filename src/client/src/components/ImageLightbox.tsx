import { useEffect, useState, useCallback } from 'react';
import { X, ChevronLeft, ChevronRight, ZoomIn, ZoomOut } from 'lucide-react';

export interface LightboxImage {
  src: string;
  alt?: string;
  caption?: string;
}

interface Props {
  /** All images to navigate through. */
  images: LightboxImage[];
  /** Index of the currently shown image. */
  index: number;
  /** Whether the lightbox is open. */
  visible: boolean;
  onClose: () => void;
  onIndexChange: (index: number) => void;
}

/**
 * Full-screen modal that shows an image at its natural size, clamped to
 * the viewport, with optional prev/next navigation across an array.
 *
 * Controls:
 *   - Click backdrop / X / Esc to close
 *   - Left / Right arrow keys to navigate (when more than one image)
 *   - Mouse wheel / +/- keys / pinch-equivalent +- buttons to zoom
 *   - Drag to pan when zoomed
 */
export function LightboxCarousel({ images, index, visible, onClose, onIndexChange }: Props) {
  const [zoom, setZoom] = useState(1);
  const [offset, setOffset] = useState({ x: 0, y: 0 });
  const [dragging, setDragging] = useState<{ startX: number; startY: number; baseX: number; baseY: number } | null>(null);

  const safeIndex = clampIndex(index, images.length);
  const current = images[safeIndex];
  const hasPrev = safeIndex > 0;
  const hasNext = safeIndex < images.length - 1;

  // Reset zoom + pan when the visible image changes.
  useEffect(() => {
    setZoom(1);
    setOffset({ x: 0, y: 0 });
  }, [safeIndex, visible]);

  const goPrev = useCallback(() => {
    if (hasPrev) onIndexChange(safeIndex - 1);
  }, [hasPrev, safeIndex, onIndexChange]);

  const goNext = useCallback(() => {
    if (hasNext) onIndexChange(safeIndex + 1);
  }, [hasNext, safeIndex, onIndexChange]);

  const zoomIn = useCallback(() => setZoom((z) => Math.min(z * 1.25, 8)), []);
  const zoomOut = useCallback(() => {
    setZoom((z) => {
      const next = Math.max(z / 1.25, 1);
      if (next === 1) setOffset({ x: 0, y: 0 });
      return next;
    });
  }, []);

  // Keyboard shortcuts: only active while visible.
  useEffect(() => {
    if (!visible) return;
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') { e.preventDefault(); onClose(); return; }
      if (e.key === 'ArrowLeft') { e.preventDefault(); goPrev(); return; }
      if (e.key === 'ArrowRight') { e.preventDefault(); goNext(); return; }
      if (e.key === '+' || e.key === '=') { e.preventDefault(); zoomIn(); return; }
      if (e.key === '-' || e.key === '_') { e.preventDefault(); zoomOut(); return; }
      if (e.key === '0') { e.preventDefault(); setZoom(1); setOffset({ x: 0, y: 0 }); return; }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [visible, onClose, goPrev, goNext, zoomIn, zoomOut]);

  const onWheel = useCallback((e: React.WheelEvent) => {
    e.preventDefault();
    if (e.deltaY < 0) setZoom((z) => Math.min(z * 1.1, 8));
    else setZoom((z) => {
      const n = Math.max(z / 1.1, 1);
      if (n === 1) setOffset({ x: 0, y: 0 });
      return n;
    });
  }, []);

  const onPointerDown = useCallback((e: React.PointerEvent) => {
    if (zoom === 1) return;
    setDragging({ startX: e.clientX, startY: e.clientY, baseX: offset.x, baseY: offset.y });
    (e.target as Element).setPointerCapture(e.pointerId);
  }, [zoom, offset]);

  const onPointerMove = useCallback((e: React.PointerEvent) => {
    if (!dragging) return;
    setOffset({
      x: dragging.baseX + (e.clientX - dragging.startX),
      y: dragging.baseY + (e.clientY - dragging.startY),
    });
  }, [dragging]);

  const onPointerUp = useCallback((e: React.PointerEvent) => {
    setDragging(null);
    try { (e.target as Element).releasePointerCapture(e.pointerId); } catch { /* ignore */ }
  }, []);

  if (!visible || !current) return null;

  return (
    <div
      className="lightbox-overlay"
      onClick={onClose}
      role="dialog"
      aria-modal="true"
      aria-label={current.alt ?? 'Image preview'}
    >
      <button
        className="lightbox-close"
        onClick={onClose}
        title="Close (Esc)"
        aria-label="Close image preview"
      >
        <X size={20} />
      </button>

      <div className="lightbox-toolbar" onClick={(e) => e.stopPropagation()}>
        <button className="lightbox-tool" onClick={zoomOut} title="Zoom out (-)" disabled={zoom <= 1}>
          <ZoomOut size={16} />
        </button>
        <span className="lightbox-zoom-label">{Math.round(zoom * 100)}%</span>
        <button className="lightbox-tool" onClick={zoomIn} title="Zoom in (+)" disabled={zoom >= 8}>
          <ZoomIn size={16} />
        </button>
        {images.length > 1 && (
          <span className="lightbox-counter">{safeIndex + 1} / {images.length}</span>
        )}
      </div>

      {hasPrev && (
        <button
          className="lightbox-nav lightbox-nav-prev"
          onClick={(e) => { e.stopPropagation(); goPrev(); }}
          title="Previous (\u2190)"
          aria-label="Previous image"
        >
          <ChevronLeft size={28} />
        </button>
      )}
      {hasNext && (
        <button
          className="lightbox-nav lightbox-nav-next"
          onClick={(e) => { e.stopPropagation(); goNext(); }}
          title="Next (\u2192)"
          aria-label="Next image"
        >
          <ChevronRight size={28} />
        </button>
      )}

      <div className="lightbox-content" onClick={(e) => e.stopPropagation()}>
        <img
          className="lightbox-image"
          src={current.src}
          alt={current.alt ?? ''}
          draggable={false}
          style={{
            transform: `translate(${offset.x}px, ${offset.y}px) scale(${zoom})`,
            cursor: zoom > 1 ? (dragging ? 'grabbing' : 'grab') : 'default',
            transition: dragging ? 'none' : 'transform 0.15s ease-out',
          }}
          onWheel={onWheel}
          onPointerDown={onPointerDown}
          onPointerMove={onPointerMove}
          onPointerUp={onPointerUp}
          onPointerCancel={onPointerUp}
        />
        {current.caption && <div className="lightbox-caption">{current.caption}</div>}
      </div>
    </div>
  );
}

function clampIndex(index: number, count: number): number {
  if (count === 0) return 0;
  if (index < 0) return 0;
  if (index >= count) return count - 1;
  return index;
}

/**
 * Single-image convenience wrapper - preserves the old `ImageLightbox`
 * call-site shape so existing code doesn't have to manage an `images`
 * array when there's only one image.
 */
interface SingleProps {
  src: string;
  alt?: string;
  caption?: string;
  visible: boolean;
  onClose: () => void;
}

export function ImageLightbox({ src, alt, caption, visible, onClose }: SingleProps) {
  return (
    <LightboxCarousel
      images={[{ src, alt, caption }]}
      index={0}
      visible={visible}
      onClose={onClose}
      onIndexChange={() => { /* single image - noop */ }}
    />
  );
}
