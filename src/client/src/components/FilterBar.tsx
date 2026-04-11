import { useState } from 'react';
import { Search, X, Filter } from 'lucide-react';
import type { EntryFilters, DashboardStats } from '../types';

interface Props {
  filters: EntryFilters;
  onChange: (filters: EntryFilters) => void;
  types: string[];
  sources: string[];
  /** Stats for showing entry counts per filter value */
  stats?: DashboardStats | null;
}

export function FilterBar({ filters, onChange, types, sources, stats }: Props) {
  const [expanded, setExpanded] = useState(false);
  const hasFilters = !!(filters.type || filters.severity || filters.source || filters.tags || filters.search);

  const update = (partial: Partial<EntryFilters>) => {
    onChange({ ...filters, ...partial });
  };

  const clear = () => {
    onChange({});
  };

  const typeCount = (t: string) => stats?.countByType?.[t];
  const severityCount = (s: string) => stats?.countBySeverity?.[s];
  const sourceCount = (s: string) => stats?.countBySource?.[s];

  const formatOption = (label: string, count: number | undefined) =>
    count !== undefined ? `${label} (${count})` : label;

  return (
    <div className="filter-bar">
      <div className="filter-bar-main">
        <div className="filter-search">
          <Search size={14} />
          <input
            type="text"
            placeholder="Search entries..."
            value={filters.search ?? ''}
            onChange={(e) => update({ search: e.target.value || undefined })}
            className="filter-search-input"
          />
          {filters.search && (
            <button className="filter-clear-btn" onClick={() => update({ search: undefined })}>
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
                <option key={t} value={t}>{formatOption(t, typeCount(t))}</option>
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
              <option value="critical">{formatOption('Critical', severityCount('critical'))}</option>
              <option value="high">{formatOption('High', severityCount('high'))}</option>
              <option value="medium">{formatOption('Medium', severityCount('medium'))}</option>
              <option value="low">{formatOption('Low', severityCount('low'))}</option>
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
                <option key={s} value={s}>{formatOption(s, sourceCount(s))}</option>
              ))}
            </select>
          </div>
          <div className="filter-group">
            <label className="filter-label">Tags</label>
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
