import { useState, useRef, useCallback } from 'react';
import type { ContentBlock } from '../../types';
import { rewriteImageUrl } from '../../utils/imageUrl';

interface Props {
  block: ContentBlock;
}

/**
 * Before/after image slider. The user drags a vertical handle to reveal
 * the underlying "after" image. Useful for visual diffs (UI regressions,
 * config visualisations) and any side-by-side comparison.
 *
 * Both images are stacked at full size; we clip the "after" image with
 * clip-path: inset() based on the slider's current X position.
 */
export function BeforeAfterBlock({ block }: Props) {
  const before = block.beforeUrl ? rewriteImageUrl(block.beforeUrl) : null;
  const after = block.afterUrl ? rewriteImageUrl(block.afterUrl) : null;
  const [pct, setPct] = useState(50);
  const containerRef = useRef<HTMLDivElement>(null);
  const dragging = useRef(false);

  const updateFromX = useCallback((clientX: number) => {
    const rect = containerRef.current?.getBoundingClientRect();
    if (!rect) return;
    const x = clientX - rect.left;
    const next = Math.max(0, Math.min(100, (x / rect.width) * 100));
    setPct(next);
  }, []);

  const onPointerDown = (e: React.PointerEvent) => {
    dragging.current = true;
    (e.target as Element).setPointerCapture(e.pointerId);
    updateFromX(e.clientX);
  };
  const onPointerMove = (e: React.PointerEvent) => {
    if (!dragging.current) return;
    updateFromX(e.clientX);
  };
  const onPointerUp = (e: React.PointerEvent) => {
    dragging.current = false;
    try { (e.target as Element).releasePointerCapture(e.pointerId); } catch { /* ignore */ }
  };

  if (!before || !after) {
    return (
      <div className="block-before-after block-before-after-missing">
        <div className="block-before-after-missing-msg">beforeAfter block needs both beforeUrl and afterUrl.</div>
      </div>
    );
  }

  return (
    <div className="block-before-after">
      {block.label && <h4 className="block-label">{block.label}</h4>}
      <div
        className="ba-frame"
        ref={containerRef}
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={onPointerUp}
        onPointerCancel={onPointerUp}
      >
        <img className="ba-img ba-img-after" src={after} alt={block.afterLabel ?? 'after'} draggable={false} />
        <div className="ba-clip" style={{ clipPath: `inset(0 ${100 - pct}% 0 0)` }}>
          <img className="ba-img ba-img-before" src={before} alt={block.beforeLabel ?? 'before'} draggable={false} />
        </div>
        <div className="ba-handle" style={{ left: `${pct}%` }} aria-label="Before/after slider handle">
          <span className="ba-handle-line" />
          <span className="ba-handle-knob">\u2194</span>
        </div>
        <span className="ba-label ba-label-before">{block.beforeLabel ?? 'Before'}</span>
        <span className="ba-label ba-label-after">{block.afterLabel ?? 'After'}</span>
      </div>
      {block.caption && <div className="ba-caption">{block.caption}</div>}
    </div>
  );
}
