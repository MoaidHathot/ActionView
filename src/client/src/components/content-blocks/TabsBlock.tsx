import { useState } from 'react';
import type { ContentBlock } from '../../types';
import { BlockRenderer } from './BlockRenderer';
import { EntryErrorBoundary } from '../EntryErrorBoundary';

interface Props {
  block: ContentBlock;
  entryId: string;
}

/**
 * Tab strip + active-panel renderer.
 *
 * Each tab is `{ label, content: ContentBlock[], badge? }`. The active
 * tab's content is rendered through BlockRenderer so any nested block
 * type works inside a tab.
 */
export function TabsBlock({ block, entryId }: Props) {
  const tabs = block.tabs ?? [];
  const [active, setActive] = useState(0);
  if (tabs.length === 0) return null;
  const safeActive = active < tabs.length ? active : 0;
  const current = tabs[safeActive];
  return (
    <div className="block-tabs">
      {block.label && <h4 className="block-label">{block.label}</h4>}
      <div className="tabs-strip" role="tablist">
        {tabs.map((tab, i) => (
          <button
            key={i}
            type="button"
            role="tab"
            aria-selected={i === safeActive}
            className={`tab-btn ${i === safeActive ? 'tab-btn-active' : ''}`}
            onClick={() => setActive(i)}
          >
            <span>{tab.label}</span>
            {tab.badge && <span className="tab-badge">{tab.badge}</span>}
          </button>
        ))}
      </div>
      <div className="tabs-panel" role="tabpanel">
        {current.content?.map((child, i) => (
          <EntryErrorBoundary key={i} label={`tab block #${i + 1}`}>
            <BlockRenderer block={child} entryId={entryId} />
          </EntryErrorBoundary>
        ))}
      </div>
    </div>
  );
}
