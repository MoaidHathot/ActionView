import { useState, useEffect, useCallback, useMemo } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { CheckCircle, XCircle, Inbox } from 'lucide-react';
import type { Entry, EntryFilters } from '../types';
import { api } from '../api/client';
import { formatDistanceToNow } from '../utils/time';
import { highlightText } from '../utils/highlight';
import { BlockRenderer } from './content-blocks/BlockRenderer';
import { FilterBar } from './FilterBar';
import { useTimestampRefresh } from '../hooks/useRelativeTime';

export function HistoryView() {
  const { entryId } = useParams<{ entryId?: string }>();
  const navigate = useNavigate();
  const [entries, setEntries] = useState<Entry[]>([]);
  const [selectedEntry, setSelectedEntry] = useState<Entry | null>(null);
  const [loading, setLoading] = useState(true);
  const [filters, setFilters] = useState<EntryFilters>({});
  const timestampTick = useTimestampRefresh(30_000);

  // suppress unused warning
  void timestampTick;

  const uniqueTypes = useMemo(
    () => [...new Set(entries.map((e) => e.type))].sort(),
    [entries],
  );
  const uniqueSources = useMemo(
    () => [...new Set(entries.map((e) => e.source))].sort(),
    [entries],
  );

  const loadHistory = useCallback(async () => {
    setLoading(true);
    try {
      const data = await api.getHistory(filters);
      setEntries(data);
    } catch (err) {
      console.error('Failed to load history:', err);
    } finally {
      setLoading(false);
    }
  }, [filters]);

  useEffect(() => {
    loadHistory();
  }, [loadHistory]);

  // Load entry from URL param
  useEffect(() => {
    if (entryId) {
      api.getHistoryEntry(entryId)
        .then((entry) => setSelectedEntry(entry))
        .catch(() => {
          setSelectedEntry(null);
          navigate('/history', { replace: true });
        });
    } else {
      setSelectedEntry(null);
    }
  }, [entryId, navigate]);

  const handleSelectEntry = useCallback((entry: Entry) => {
    setSelectedEntry(entry);
    navigate(`/history/${entry.id}`);
  }, [navigate]);

  return (
    <div className="history-view">
      <div className="entry-list">
        <FilterBar
          filters={filters}
          onChange={setFilters}
          types={uniqueTypes}
          sources={uniqueSources}
        />
        {loading ? (
          <div className="loading">Loading history...</div>
        ) : entries.length === 0 ? (
          <div className="entry-list-empty">
            <Inbox size={40} strokeWidth={1.2} />
            <p className="empty-title">No history yet</p>
            <p className="subtle">Entries that are dismissed, actioned, or archived will appear here.</p>
          </div>
        ) : (
          entries.map((entry) => (
            <div
              key={entry.id}
              className={`entry-list-item history-item ${selectedEntry?.id === entry.id ? 'selected' : ''}`}
              onClick={() => handleSelectEntry(entry)}
            >
              <div className="entry-list-item-indicator">
                {entry.outcome?.success ? (
                  <CheckCircle size={14} className="outcome-success" />
                ) : (
                  <XCircle size={14} className="outcome-failed" />
                )}
              </div>
              <div className="entry-list-item-content">
                <div className="entry-list-item-title">
                  {highlightText(entry.title, filters.search)}
                </div>
                <div className="entry-list-item-meta">
                  <span className="outcome-action">{entry.outcome?.action}</span>
                  <span className="entry-time">
                    {entry.outcome?.timestamp
                      ? formatDistanceToNow(entry.outcome.timestamp)
                      : formatDistanceToNow(entry.createdAt)}
                  </span>
                </div>
              </div>
            </div>
          ))
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
