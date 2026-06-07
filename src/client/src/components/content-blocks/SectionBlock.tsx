import { useState, useEffect, useRef } from 'react';
import { ChevronRight } from 'lucide-react';
import type { ContentBlock } from '../../types';
import { BlockRenderer } from './BlockRenderer';
import { ActionButton } from '../ActionButton';
import { EntryErrorBoundary } from '../EntryErrorBoundary';

interface Props {
  block: ContentBlock;
  entryId: string;
  sectionIndex: number;
  onAction?: (sectionIndex: number, actionIndex: number, parameters?: Record<string, string>) => void;
}

/**
 * Collapsible group of nested blocks with optional scoped actions.
 *
 * Improvements vs the previous version:
 *   - block.defaultCollapsed honored (default expanded)
 *   - block.badge rendered next to the title (e.g. "11 sources")
 *   - Collapsed state persisted in localStorage per entry+section
 *   - Smooth expand/collapse via grid-template-rows transition
 *   - Header is a real <button> (keyboard accessible)
 */
export function SectionBlock({ block, entryId, sectionIndex, onAction }: Props) {
  const storageKey = `actionview.section-collapsed.${entryId}.${sectionIndex}`;
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
      </button>

      <div className="section-content-wrap" hidden={!expanded}>
        <div className="section-content">
          {block.content?.map((child, i) => (
            <EntryErrorBoundary key={i} label={`section block #${i + 1}`}>
              <BlockRenderer block={child} entryId={entryId} />
            </EntryErrorBoundary>
          ))}
          {block.actions && block.actions.length > 0 && (
            <div className="section-actions">
              {block.actions.map((action, actionIdx) => (
                <ActionButton
                  key={actionIdx}
                  action={action}
                  draftKey={`${entryId}.s${sectionIndex}.${actionIdx}`}
                  onClick={(parameters) => onAction?.(sectionIndex, actionIdx, parameters)}
                />
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
