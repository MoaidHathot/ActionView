import { useState } from 'react';
import { X, Archive, Trash2, Zap } from 'lucide-react';
import { api } from '../api/client';

interface Props {
  selectedIds: Set<string>;
  /** Common action labels shared across all selected entries */
  commonActions: string[];
  onClearSelection: () => void;
  onBatchComplete: () => void;
}

export function BatchActionBar({
  selectedIds,
  commonActions,
  onClearSelection,
  onBatchComplete,
}: Props) {
  const [executing, setExecuting] = useState(false);
  const count = selectedIds.size;

  if (count === 0) return null;

  const ids = Array.from(selectedIds);

  const handleDismiss = async () => {
    setExecuting(true);
    try {
      await api.batchDismiss(ids);
      onBatchComplete();
    } catch (err) {
      console.error('Batch dismiss failed:', err);
    } finally {
      setExecuting(false);
    }
  };

  const handleDelete = async () => {
    if (!window.confirm(`Permanently delete ${count} entries?`)) return;
    setExecuting(true);
    try {
      await api.batchDelete(ids);
      onBatchComplete();
    } catch (err) {
      console.error('Batch delete failed:', err);
    } finally {
      setExecuting(false);
    }
  };

  const handleAction = async (actionLabel: string) => {
    if (!window.confirm(`Execute "${actionLabel}" on ${count} entries?`)) return;
    setExecuting(true);
    try {
      await api.batchAction(ids, actionLabel);
      onBatchComplete();
    } catch (err) {
      console.error('Batch action failed:', err);
    } finally {
      setExecuting(false);
    }
  };

  return (
    <div className="batch-bar">
      <div className="batch-bar-info">
        <span className="batch-count">{count} selected</span>
        <button className="batch-clear" onClick={onClearSelection} title="Clear selection">
          <X size={14} />
        </button>
      </div>
      <div className="batch-bar-actions">
        {commonActions.map((label) => (
          <button
            key={label}
            className="action-btn action-primary batch-action-btn"
            onClick={() => handleAction(label)}
            disabled={executing}
          >
            <Zap size={14} /> {label}
          </button>
        ))}
        <button
          className="action-btn action-default"
          onClick={handleDismiss}
          disabled={executing}
        >
          <Archive size={14} /> Dismiss All
        </button>
        <button
          className="action-btn action-danger-outline"
          onClick={handleDelete}
          disabled={executing}
        >
          <Trash2 size={14} /> Delete All
        </button>
      </div>
    </div>
  );
}
