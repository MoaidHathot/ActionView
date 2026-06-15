import { ArrowDown, ArrowUp } from 'lucide-react';
import type { SortField, SortOption } from '../types';

interface Props {
  sort: SortOption;
  onChange: (sort: SortOption) => void;
}

const FIELD_LABELS: Record<SortField, string> = {
  default: 'Default',
  created: 'Created',
  priority: 'Priority',
  severity: 'Severity',
  title: 'Title',
};

/**
 * Compact sort control: a field dropdown plus a direction toggle. The direction
 * button is disabled for the "Default" field (the server's canonical order).
 */
export function SortControl({ sort, onChange }: Props) {
  const isDefault = sort.field === 'default';

  return (
    <div className="sort-control">
      <select
        className="sort-field"
        value={sort.field}
        onChange={(e) => onChange({ ...sort, field: e.target.value as SortField })}
        title="Sort by"
        aria-label="Sort field"
      >
        {(Object.keys(FIELD_LABELS) as SortField[]).map((f) => (
          <option key={f} value={f}>{FIELD_LABELS[f]}</option>
        ))}
      </select>
      <button
        type="button"
        className="sort-dir"
        disabled={isDefault}
        onClick={() => onChange({ ...sort, direction: sort.direction === 'asc' ? 'desc' : 'asc' })}
        title={sort.direction === 'asc' ? 'Ascending' : 'Descending'}
        aria-label={`Sort direction: ${sort.direction === 'asc' ? 'ascending' : 'descending'}`}
      >
        {sort.direction === 'asc' ? <ArrowUp size={14} /> : <ArrowDown size={14} />}
      </button>
    </div>
  );
}
