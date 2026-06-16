import { useState, useEffect } from 'react';
import { Search, X, Filter } from 'lucide-react';
import type { EntryFilters, SortOption, TagMatchMode } from '../types';
import { SortControl } from './SortControl';

// How long to wait after the last keystroke before applying the search filter.
const SEARCH_DEBOUNCE_MS = 250;

interface Props {
  filters: EntryFilters;
  onChange: (filters: EntryFilters) => void;
  types: string[];
  sources: string[];
  defaultTagMode?: TagMatchMode;
  sort?: SortOption;
  onSortChange?: (sort: SortOption) => void;
}

export function FilterBar({
  filters, onChange, types, sources, defaultTagMode = 'any', sort, onSortChange,
}: Props) {
  const [expanded, setExpanded] = useState(false);

  // Local, immediately-responsive copy of the search box. The value is only
  // pushed up to `filters` (which triggers a refetch) after the user pauses
  // typing, so we don't hit the API on every keystroke.
  const [searchText, setSearchText] = useState(filters.search ?? '');
  const [lastExternalSearch, setLastExternalSearch] = useState(filters.search ?? '');

  // If the search changes externally (Clear button, applying a saved view),
  // reset the local box. Adjusting state during render is React's recommended
  // alternative to a prop->state sync effect.
  const externalSearch = filters.search ?? '';
  if (externalSearch !== lastExternalSearch) {
    setLastExternalSearch(externalSearch);
    setSearchText(externalSearch);
  }

  // Debounced propagation: the timer resets on every change and only fires once
  // typing settles.
  useEffect(() => {
    const next = searchText || undefined;
    if ((filters.search ?? '') === (next ?? '')) return;
    const handle = setTimeout(() => onChange({ ...filters, search: next }), SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(handle);
  }, [searchText, filters, onChange]);

  const hasFilters = !!(filters.type || filters.severity || filters.source || filters.tags || searchText);

  const update = (partial: Partial<EntryFilters>) => {
    onChange({ ...filters, ...partial });
  };

  const clear = () => {
    setSearchText('');
    onChange({});
  };

  // Clearing the search box applies immediately (no debounce wait).
  const clearSearch = () => {
    setSearchText('');
    onChange({ ...filters, search: undefined });
  };

  // Effective tag-match mode shown on the toggle: explicit override, else the
  // server's configured default.
  const effectiveTagMode: TagMatchMode = filters.tagMode ?? defaultTagMode;

  return (
    <div className="filter-bar">
      <div className="filter-bar-main">
        <div className="filter-search">
          <Search size={14} />
          <input
            type="text"
            placeholder="Search entries..."
            value={searchText}
            onChange={(e) => setSearchText(e.target.value)}
            className="filter-search-input"
          />
          {searchText && (
            <button className="filter-clear-btn" onClick={clearSearch}>
              <X size={12} />
            </button>
          )}
        </div>
        <button
          className={`filter-toggle-btn ${expanded ? 'active' : ''}`}
          onClick={() => setExpanded(!expanded)}
          title="Toggle filters"
        >
          <Filter size={14} />
          {hasFilters && <span className="filter-indicator" />}
        </button>
        {hasFilters && (
          <button className="filter-clear-all" onClick={clear}>
            Clear
          </button>
        )}
        {sort && onSortChange && (
          <SortControl sort={sort} onChange={onSortChange} />
        )}
      </div>

      {expanded && (
        <div className="filter-bar-expanded">
          <div className="filter-group">
            <label className="filter-label">Type</label>
            <select
              className="filter-select"
              value={filters.type ?? ''}
              onChange={(e) => update({ type: e.target.value || undefined })}
            >
              <option value="">All</option>
              {types.map((t) => (
                <option key={t} value={t}>{t}</option>
              ))}
            </select>
          </div>
          <div className="filter-group">
            <label className="filter-label">Severity</label>
            <select
              className="filter-select"
              value={filters.severity ?? ''}
              onChange={(e) => update({ severity: e.target.value || undefined })}
            >
              <option value="">All</option>
              <option value="critical">Critical</option>
              <option value="high">High</option>
              <option value="medium">Medium</option>
              <option value="low">Low</option>
            </select>
          </div>
          <div className="filter-group">
            <label className="filter-label">Source</label>
            <select
              className="filter-select"
              value={filters.source ?? ''}
              onChange={(e) => update({ source: e.target.value || undefined })}
            >
              <option value="">All</option>
              {sources.map((s) => (
                <option key={s} value={s}>{s}</option>
              ))}
            </select>
          </div>
          <div className="filter-group">
            <label className="filter-label">
              Tags
              <span className="tag-mode-toggle" role="group" aria-label="Tag match mode">
                <button
                  type="button"
                  className={effectiveTagMode === 'any' ? 'active' : ''}
                  onClick={() => update({ tagMode: 'any' })}
                  title="Match entries with ANY of these tags"
                >
                  Any
                </button>
                <button
                  type="button"
                  className={effectiveTagMode === 'all' ? 'active' : ''}
                  onClick={() => update({ tagMode: 'all' })}
                  title="Match entries with ALL of these tags"
                >
                  All
                </button>
              </span>
            </label>
            <input
              type="text"
              className="filter-input"
              placeholder="tag1,tag2"
              value={filters.tags ?? ''}
              onChange={(e) => update({ tags: e.target.value || undefined })}
            />
          </div>
        </div>
      )}
    </div>
  );
}
