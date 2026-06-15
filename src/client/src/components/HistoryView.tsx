import { useState, useEffect, useCallback, useMemo } from 'react';
import { Clock, CheckCircle, XCircle } from 'lucide-react';
import type { Entry, EntryFilters, SavedView, SortOption, TagMatchMode, ViewCounts } from '../types';
import { api } from '../api/client';
import { formatDistanceToNow } from '../utils/time';
import { BlockRenderer } from './content-blocks/BlockRenderer';
import { FilterBar } from './FilterBar';
import { ViewBar } from './ViewBar';
import { useViewBinding, type NewView } from '../hooks/useViews';

interface Props {
  views: SavedView[];
  createView: (partial: NewView) => Promise<SavedView | undefined>;
  deleteView: (id: string) => Promise<void>;
  defaultTagMode: TagMatchMode;
  counts?: ViewCounts | null;
  replaceViews: (views: SavedView[]) => Promise<SavedView[] | undefined>;
}

const MAX_VISIBLE_TAGS = 4;
const PAGE_SIZE = 50;

export function HistoryView({ views, createView, deleteView, defaultTagMode, counts, replaceViews }: Props) {
  const [entries, setEntries] = useState<Entry[]>([]);
  const [selectedEntry, setSelectedEntry] = useState<Entry | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [hasMore, setHasMore] = useState(false);
  const [filters, setFilters] = useState<EntryFilters>({});
  const [sort, setSort] = useState<SortOption>({ field: 'default', direction: 'desc' });

  const binding = useViewBinding(filters, setFilters, views, createView, deleteView);

  const uniqueTypes = useMemo(
    () => [...new Set(entries.map((e) => e.type))].sort(),
    [entries],
  );
  const uniqueSources = useMemo(
    () => [...new Set(entries.map((e) => e.source))].sort(),
    [entries],
  );

  const loadPage = useCallback(async (offset: number, append: boolean) => {
    if (append) setLoadingMore(true);
    else setLoading(true);
    try {
      const data = await api.getHistory(filters, PAGE_SIZE, offset, sort);
      setEntries((prev) => (append ? [...prev, ...data] : data));
      setHasMore(data.length === PAGE_SIZE);
    } catch (err) {
      console.error('Failed to load history:', err);
    } finally {
      if (append) setLoadingMore(false);
      else setLoading(false);
    }
  }, [filters, sort]);

  // Reload the first page whenever filters or sort change.
  useEffect(() => {
    loadPage(0, false);
  }, [loadPage]);

  return (
    <div className="history-view">
      <div className="entry-list">
        <ViewBar
          views={views}
          activeId={binding.currentViewId}
          currentFilters={filters}
          onApplyAll={binding.onApplyAll}
          onApplyView={binding.onApplyView}
          onCreate={binding.onCreate}
          onDelete={binding.onDelete}
          counts={counts}
          onSaveViews={replaceViews}
        />
        <FilterBar
          filters={filters}
          onChange={setFilters}
          types={uniqueTypes}
          sources={uniqueSources}
          defaultTagMode={defaultTagMode}
          sort={sort}
          onSortChange={setSort}
        />
        {loading ? (
          <div className="loading">Loading history...</div>
        ) : entries.length === 0 ? (
          <div className="entry-list-empty">
            <Clock size={32} />
            <p>No history yet</p>
          </div>
        ) : (
          <>
            {entries.map((entry) => (
              <div
                key={entry.id}
                className={`entry-list-item history-item ${selectedEntry?.id === entry.id ? 'selected' : ''}`}
                onClick={() => setSelectedEntry(entry)}
              >
                <div className="entry-list-item-indicator">
                  {entry.outcome?.success ? (
                    <CheckCircle size={14} className="outcome-success" />
                  ) : (
                    <XCircle size={14} className="outcome-failed" />
                  )}
                </div>
                <div className="entry-list-item-content">
                  <div className="entry-list-item-title">{entry.title}</div>
                  <div className="entry-list-item-meta">
                    <span className="outcome-action">{entry.outcome?.action}</span>
                    <span className="entry-time">
                      {entry.outcome?.timestamp
                        ? formatDistanceToNow(entry.outcome.timestamp)
                        : formatDistanceToNow(entry.createdAt)}
                    </span>
                  </div>
                  {entry.tags.length > 0 && (
                    <div className="entry-list-item-tags">
                      {entry.tags.slice(0, MAX_VISIBLE_TAGS).map((tag) => (
                        <span
                          key={tag}
                          className="entry-tag clickable"
                          title={`Filter by "${tag}"`}
                          onClick={(e) => { e.stopPropagation(); binding.onTagClick(tag); }}
                        >
                          {tag}
                        </span>
                      ))}
                      {entry.tags.length > MAX_VISIBLE_TAGS && (
                        <span className="entry-tag-more" title={entry.tags.slice(MAX_VISIBLE_TAGS).join(', ')}>
                          +{entry.tags.length - MAX_VISIBLE_TAGS}
                        </span>
                      )}
                    </div>
                  )}
                </div>
              </div>
            ))}
            {hasMore && (
              <button
                type="button"
                className="history-load-more"
                onClick={() => loadPage(entries.length, true)}
                disabled={loadingMore}
              >
                {loadingMore ? 'Loading\u2026' : 'Load more'}
              </button>
            )}
          </>
        )}
      </div>

      {selectedEntry && (
        <div className="entry-detail history-detail">
          <div className="entry-detail-header">
            <h2>{selectedEntry.title}</h2>
            {selectedEntry.subtitle && (
              <p className="entry-detail-subtitle">{selectedEntry.subtitle}</p>
            )}
            {selectedEntry.outcome && (
              <div className={`outcome-banner ${selectedEntry.outcome.success ? 'success' : 'failed'}`}>
                <span className="outcome-label">
                  {selectedEntry.outcome.success ? <CheckCircle size={16} /> : <XCircle size={16} />}
                  {selectedEntry.outcome.action}
                </span>
                <span className="outcome-time">
                  {new Date(selectedEntry.outcome.timestamp).toLocaleString()}
                </span>
                {selectedEntry.outcome.resultMessage && (
                  <span className="outcome-message">{selectedEntry.outcome.resultMessage}</span>
                )}
              </div>
            )}
          </div>
          <div className="entry-detail-content">
            {selectedEntry.content.map((block, i) => (
              <BlockRenderer key={i} block={block} entryId={selectedEntry.id} />
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
