import { useState, useEffect, useRef } from 'react';
import { RotateCcw, X } from 'lucide-react';
import { api } from '../api/client';

export interface UndoItem {
  id: number;
  entryId: string;
  entryTitle: string;
  actionLabel: string;
  windowSeconds: number;
  createdAt: number;
}

let nextUndoId = 0;

export function createUndoItem(
  entryId: string,
  entryTitle: string,
  actionLabel: string,
  windowSeconds: number,
): UndoItem {
  return {
    id: nextUndoId++,
    entryId,
    entryTitle,
    actionLabel,
    windowSeconds,
    createdAt: Date.now(),
  };
}

interface Props {
  items: UndoItem[];
  onDismiss: (id: number) => void;
  onUndoComplete: () => void;
}

export function UndoToastContainer({ items, onDismiss, onUndoComplete }: Props) {
  if (items.length === 0) return null;

  return (
    <div className="undo-container">
      {items.map((item) => (
        <UndoToast
          key={item.id}
          item={item}
          onDismiss={() => onDismiss(item.id)}
          onUndoComplete={onUndoComplete}
        />
      ))}
    </div>
  );
}

function UndoToast({
  item,
  onDismiss,
  onUndoComplete,
}: {
  item: UndoItem;
  onDismiss: () => void;
  onUndoComplete: () => void;
}) {
  const [remaining, setRemaining] = useState(item.windowSeconds);
  const [undoing, setUndoing] = useState(false);
  const timerRef = useRef<ReturnType<typeof setInterval> | undefined>(undefined);

  useEffect(() => {
    timerRef.current = setInterval(() => {
      const elapsed = (Date.now() - item.createdAt) / 1000;
      const left = Math.max(0, item.windowSeconds - elapsed);
      setRemaining(Math.ceil(left));
      if (left <= 0) {
        clearInterval(timerRef.current);
        onDismiss();
      }
    }, 250);

    return () => clearInterval(timerRef.current);
  }, [item.createdAt, item.windowSeconds, onDismiss]);

  const handleUndo = async () => {
    setUndoing(true);
    try {
      await api.undoEntry(item.entryId);
      onUndoComplete();
    } catch (err) {
      console.error('Undo failed:', err);
    } finally {
      setUndoing(false);
      onDismiss();
    }
  };

  const progress = remaining / item.windowSeconds;

  return (
    <div className="undo-toast">
      <div className="undo-toast-content">
        <RotateCcw size={14} className="undo-icon" />
        <div className="undo-toast-body">
          <span className="undo-toast-title">
            {item.actionLabel}: {item.entryTitle}
          </span>
          <span className="undo-toast-timer">{remaining}s</span>
        </div>
        <button
          className="action-btn action-primary undo-btn"
          onClick={handleUndo}
          disabled={undoing}
        >
          {undoing ? 'Undoing...' : 'Undo'}
        </button>
        <button className="toast-close" onClick={onDismiss}>
          <X size={14} />
        </button>
      </div>
      <div className="undo-progress">
        <div className="undo-progress-bar" style={{ width: `${progress * 100}%` }} />
      </div>
    </div>
  );
}
