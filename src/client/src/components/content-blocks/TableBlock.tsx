import { useState, useMemo } from 'react';
import { ChevronUp, ChevronDown, ChevronsUpDown, Search } from 'lucide-react';
import type { ContentBlock, RichCell } from '../../types';
import { RichCellView, richCellText } from '../RichCellView';

interface Props {
  block: ContentBlock;
}

type SortDir = 'asc' | 'desc' | null;

/**
 * Table with rich cells, optional click-to-sort columns, optional
 * filter input. Cells may be plain strings or typed rich-cell objects
 * (link, status, code, copy, badge, markdown, image, text).
 *
 * Sorting and filtering operate on the plain-text representation of
 * each cell, so they work regardless of cell richness.
 */
export function TableBlock({ block }: Props) {
  const columns = block.columns ?? [];
  const rows = block.rows ?? [];

  const [sortCol, setSortCol] = useState<number | null>(null);
  const [sortDir, setSortDir] = useState<SortDir>(null);
  const [filter, setFilter] = useState('');

  const filteredRows = useMemo(() => {
    if (!filter.trim()) return rows;
    const needle = filter.trim().toLowerCase();
    return rows.filter((row) =>
      row.some((cell) => richCellText(cell).toLowerCase().includes(needle)),
    );
  }, [rows, filter]);

  const sortedRows = useMemo(() => {
    if (sortCol === null || sortDir === null) return filteredRows;
    const ordered = [...filteredRows];
    ordered.sort((a, b) => {
      const va = richCellText(a[sortCol] ?? '');
      const vb = richCellText(b[sortCol] ?? '');
      // Numeric-aware: if both parse as numbers, sort numerically.
      const na = parseFloat(va);
      const nb = parseFloat(vb);
      let cmp: number;
      if (!Number.isNaN(na) && !Number.isNaN(nb) && `${na}` === va.trim() && `${nb}` === vb.trim()) {
        cmp = na - nb;
      } else {
        cmp = va.localeCompare(vb, undefined, { numeric: true, sensitivity: 'base' });
      }
      return sortDir === 'asc' ? cmp : -cmp;
    });
    return ordered;
  }, [filteredRows, sortCol, sortDir]);

  const onHeaderClick = (i: number) => {
    if (!block.sortable) return;
    if (sortCol !== i) { setSortCol(i); setSortDir('asc'); return; }
    if (sortDir === 'asc') { setSortDir('desc'); return; }
    if (sortDir === 'desc') { setSortCol(null); setSortDir(null); return; }
    setSortDir('asc');
  };

  return (
    <div className="block-table">
      {block.label && <h4 className="block-label">{block.label}</h4>}
      {block.filterable && (
        <div className="table-filter">
          <Search size={13} />
          <input
            type="search"
            placeholder="Filter rows"
            value={filter}
            onChange={(e) => setFilter(e.target.value)}
          />
          <span className="table-filter-count">
            {filter ? `${sortedRows.length} / ${rows.length}` : `${rows.length} row${rows.length === 1 ? '' : 's'}`}
          </span>
        </div>
      )}
      <div className="table-scroll">
        <table>
          <thead>
            <tr>
              {columns.map((col, i) => (
                <th
                  key={i}
                  onClick={() => onHeaderClick(i)}
                  className={block.sortable ? 'table-th-sortable' : ''}
                  aria-sort={
                    sortCol === i
                      ? sortDir === 'asc' ? 'ascending'
                        : sortDir === 'desc' ? 'descending'
                        : 'none'
                      : 'none'
                  }
                >
                  <span>{col}</span>
                  {block.sortable && <SortIcon active={sortCol === i} dir={sortCol === i ? sortDir : null} />}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {sortedRows.length === 0 && (
              <tr>
                <td colSpan={columns.length} className="table-empty">
                  {filter ? 'No rows match the filter.' : 'No rows.'}
                </td>
              </tr>
            )}
            {sortedRows.map((row, ri) => (
              <tr key={ri}>
                {row.map((cell, ci) => (
                  <td key={ci}><RichCellView cell={cell as RichCell} /></td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function SortIcon({ active, dir }: { active: boolean; dir: SortDir }) {
  if (!active || dir === null) return <ChevronsUpDown size={12} className="table-sort-icon" />;
  if (dir === 'asc') return <ChevronUp size={12} className="table-sort-icon table-sort-icon-active" />;
  return <ChevronDown size={12} className="table-sort-icon table-sort-icon-active" />;
}
