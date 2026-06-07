import { useEffect, useRef, useState } from 'react';
import type { ContentBlock } from '../../types';

interface Props {
  block: ContentBlock;
}

/**
 * Renders a Mermaid diagram from `block.body` (Mermaid source text).
 *
 * Mermaid is heavy (~600kb) so it's loaded lazily via dynamic import the
 * first time a diagram block actually mounts; on subsequent renders the
 * cached singleton is reused.
 *
 * Failures (bad Mermaid syntax) are caught and displayed inline so a
 * malformed diagram doesn't blow up the whole entry.
 */
export function DiagramBlock({ block }: Props) {
  const source = typeof block.body === 'string' ? block.body : String(block.body ?? '');
  const ref = useRef<HTMLDivElement>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    (async () => {
      try {
        const mermaid = (await import('mermaid')).default;
        if (!mermaidInited) {
          mermaid.initialize({
            startOnLoad: false,
            theme: 'dark',
            securityLevel: 'strict',
            fontFamily: getComputedStyle(document.body).fontFamily,
          });
          mermaidInited = true;
        }
        const id = `mermaid-${diagramCounter++}`;
        const { svg } = await mermaid.render(id, source);
        if (!cancelled && ref.current) {
          ref.current.innerHTML = svg;
          setError(null);
        }
      } catch (e) {
        if (!cancelled) setError(e instanceof Error ? e.message : String(e));
      }
    })();

    return () => { cancelled = true; };
  }, [source]);

  return (
    <div className="block-diagram">
      {block.label && <h4 className="block-label">{block.label}</h4>}
      {error
        ? <div className="diagram-error">Diagram failed: {error}</div>
        : <div ref={ref} className="diagram-canvas" />}
      {block.caption && <div className="diagram-caption">{block.caption}</div>}
    </div>
  );
}

// Module-level singleton + id counter so we don't re-init mermaid for every block.
let mermaidInited = false;
let diagramCounter = 0;
