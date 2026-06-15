import { useState } from 'react';
import { Plus, X, Check } from 'lucide-react';
import type { EntryFilters, SavedView } from '../types';
import type { NewView } from '../hooks/useViews';
import { ALL_VIEW_ID } from '../utils/views';
import { renderIcon, VIEW_ICON_NAMES } from '../utils/icons';

interface Props {
  views: SavedView[];
  activeId: string | null;
  currentFilters: EntryFilters;
  onApplyAll: () => void;
  onApplyView: (view: SavedView) => void;
  onCreate: (view: NewView) => void;
  onDelete: (id: string) => void;
}

function describeFilters(filters: EntryFilters): string {
  const parts: string[] = [];
  if (filters.type) parts.push(`type: ${filters.type}`);
  if (filters.tags) parts.push(`tags: ${filters.tags} (${filters.tagMode ?? 'any'})`);
  return parts.length ? parts.join(' \u00b7 ') : 'all entries';
}

function describeView(view: SavedView): string {
  const parts: string[] = [];
  if (view.type) parts.push(`type: ${view.type}`);
  if (view.tags?.length) parts.push(`tags: ${view.tags.join(', ')} (${view.tagMatch ?? 'any'})`);
  return parts.length ? parts.join(' \u00b7 ') : 'all entries';
}

/**
 * Horizontal row of saved-view "pills" above the filter bar. Clicking a pill
 * applies that view's filters; the "+" button saves the current filters as a
 * new named view (with an optional icon). The "All" pill clears the view.
 */
export function ViewBar({
  views, activeId, currentFilters, onApplyAll, onApplyView, onCreate, onDelete,
}: Props) {
  const [adding, setAdding] = useState(false);
  const [name, setName] = useState('');
  const [icon, setIcon] = useState('');

  const cancelAdd = () => {
    setAdding(false);
    setName('');
    setIcon('');
  };

  const submitAdd = () => {
    const trimmed = name.trim();
    if (!trimmed) return;
    const tags = currentFilters.tags
      ? currentFilters.tags.split(',').map((t) => t.trim()).filter(Boolean)
      : [];
    onCreate({
      name: trimmed,
      icon: icon || undefined,
      type: currentFilters.type || undefined,
      tags,
      tagMatch: tags.length > 0 ? currentFilters.tagMode : undefined,
    });
    cancelAdd();
  };

  return (
    <div className="view-bar">
      <button
        className={`view-pill ${activeId === ALL_VIEW_ID ? 'active' : ''}`}
        onClick={onApplyAll}
        title="Show all entries"
      >
        All
      </button>

      {views.map((view) => {
        return (
          <span
            key={view.id}
            className={`view-pill ${activeId === view.id ? 'active' : ''}`}
          >
            <button
              className="view-pill-label"
              onClick={() => onApplyView(view)}
              title={describeView(view)}
            >
              {renderIcon(view.icon, 12)}
              {view.name}
            </button>
            <button
              className="view-pill-delete"
              onClick={() => onDelete(view.id)}
              title={`Delete "${view.name}"`}
              aria-label={`Delete view ${view.name}`}
            >
              <X size={11} />
            </button>
          </span>
        );
      })}

      {adding ? (
        <span className="view-add-form">
          <select
            className="view-add-icon"
            value={icon}
            onChange={(e) => setIcon(e.target.value)}
            title="Icon"
            aria-label="View icon"
          >
            <option value="">no icon</option>
            {VIEW_ICON_NAMES.map((n) => (
              <option key={n} value={n}>{n}</option>
            ))}
          </select>
          {renderIcon(icon, 14)}
          <input
            autoFocus
            className="view-add-input"
            placeholder="View name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') submitAdd();
              else if (e.key === 'Escape') cancelAdd();
            }}
          />
          <span className="view-add-hint" title="The current filters are saved into this view">
            {describeFilters(currentFilters)}
          </span>
          <button
            className="view-add-save"
            onClick={submitAdd}
            disabled={!name.trim()}
            title="Save view"
            aria-label="Save view"
          >
            <Check size={13} />
          </button>
          <button
            className="view-add-cancel"
            onClick={cancelAdd}
            title="Cancel"
            aria-label="Cancel"
          >
            <X size={13} />
          </button>
        </span>
      ) : (
        <button
          className="view-add-btn"
          onClick={() => { setName(''); setIcon(''); setAdding(true); }}
          title="Save current filters as a view"
          aria-label="Save current filters as a view"
        >
          <Plus size={13} />
        </button>
      )}
    </div>
  );
}
