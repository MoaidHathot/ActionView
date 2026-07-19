import { useState, useEffect, useRef } from 'react';
import { ChevronRight, Check, AlertTriangle } from 'lucide-react';
import type { ContentBlock, Entry, ActionJob } from '../../types';
import type { DerivedMarkers } from '../../utils/markers';
import { markerForSection } from '../../utils/markers';
import { BlockRenderer } from './BlockRenderer';
import { ActionButton } from '../ActionButton';
import { EntryErrorBoundary } from '../EntryErrorBoundary';

interface Props {
  block: ContentBlock;
  entryId: string;
  /** Positional path to THIS section (indices into content/children at each level). */
  path: number[];
  /** Executes an action owned by a block, addressed by its full path. */
  onBlockAction?: (path: number[], actionIndex: number, parameters?: Record<string, string>) => Promise<ActionJob | void> | ActionJob | void;
  /** Derived outcome markers so this section can show its last result. */
  markers?: DerivedMarkers;
  /** Called with the updated entry after a nested block is edited/reverted inline. */
  onEntryChanged?: (entry: Entry) => void;
  /** Expands {{content.*}}/{{entry.*}} in parameter defaults for display. */
  expandRef?: (text: string, self?: ContentBlock) => string;
}

/**
 * Collapsible group of nested blocks with optional scoped actions.
 *
 * Section actions are addressed by the section's full positional {@link path},
 * so actions on nested sections (e.g. per-comment Approve inside a "Review
 * Comments" section) resolve correctly on the server. The path — and the
 * onBlockAction handler — are forwarded to nested BlockRenderers so nesting
 * works at any depth.
 */
export function SectionBlock({ block, entryId, path, onBlockAction, markers, onEntryChanged, expandRef }: Props) {
  const pathId = path.join('-');
  const storageKey = `actionview.section-collapsed.${entryId}.${pathId}`;
  const initial = (() => {
    try {
      const stored = localStorage.getItem(storageKey);
      if (stored === '0') return true;       // explicit "open"
      if (stored === '1') return false;      // explicit "closed"
    } catch { /* unavailable */ }
    return !(block.defaultCollapsed ?? false);
  })();

  const [expanded, setExpanded] = useState(initial);
  // Skip persistence on the very first render (initial value already reflects storage).
  const firstRender = useRef(true);
  useEffect(() => {
    if (firstRender.current) { firstRender.current = false; return; }
    try { localStorage.setItem(storageKey, expanded ? '0' : '1'); } catch { /* ignore */ }
  }, [storageKey, expanded]);

  const title = block.title ?? block.label ?? 'Section';
  const childCount = block.content?.length ?? 0;
  const badge = block.badge
    ?? (childCount > 0 && (block.defaultCollapsed) ? `${childCount}` : undefined);

  const marker = markerForSection(markers, path, block.id);
  const canAct = typeof onBlockAction === 'function';

  return (
    <div className={`block-section ${expanded ? 'block-section-expanded' : 'block-section-collapsed'}`}>
      <button
        type="button"
        className="section-title-btn"
        onClick={() => setExpanded(v => !v)}
        aria-expanded={expanded}
      >
        <ChevronRight size={14} className="section-chevron" />
        <span className="section-title-text">{title}</span>
        {badge && <span className="section-badge">{badge}</span>}
        {marker && (
          <span
            className={`section-marker ${marker.success ? 'section-marker-success' : 'section-marker-fail'}`}
            title={`${marker.label} — ${marker.success ? 'succeeded' : 'failed'}`}
          >
            {marker.success ? <Check size={12} /> : <AlertTriangle size={12} />}
            {marker.success ? marker.label : `${marker.label} failed`}
          </span>
        )}
      </button>

      <div className="section-content-wrap" hidden={!expanded}>
        <div className="section-content">
          {block.content?.map((child, i) => (
            <EntryErrorBoundary key={i} label={`section block #${i + 1}`}>
              <BlockRenderer
                block={child}
                entryId={entryId}
                path={[...path, i]}
                onBlockAction={onBlockAction}
                markers={markers}
                onEntryChanged={onEntryChanged}
                expandRef={expandRef}
              />
            </EntryErrorBoundary>
          ))}
          {block.actions && block.actions.length > 0 && (
            <div className="section-actions">
              {block.actions.map((action, actionIdx) => (
                <ActionButton
                  key={actionIdx}
                  action={action}
                  draftKey={`${entryId}.b${pathId}.${actionIdx}`}
                  marker={marker && marker.label === action.label ? marker : undefined}
                  disabled={!canAct}
                  disabledReason={canAct ? undefined : 'This action can’t be run from here yet.'}
                  expandDefault={expandRef ? (t) => expandRef(t, block) : undefined}
                  onClick={(parameters) => onBlockAction?.(path, actionIdx, parameters)}
                />
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
