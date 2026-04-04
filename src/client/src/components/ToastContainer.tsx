import { useState, useCallback, useRef, useEffect } from 'react';
import { Bell, X } from 'lucide-react';
import type { Entry } from '../types';

interface Toast {
  id: number;
  entries: Entry[];
  timestamp: number;
}

let nextId = 0;

export function useToasts() {
  const [toasts, setToasts] = useState<Toast[]>([]);
  const timersRef = useRef<Map<number, ReturnType<typeof setTimeout>>>(new Map());

  const addToast = useCallback((entries: Entry[]) => {
    const id = nextId++;
    setToasts((prev) => [...prev, { id, entries, timestamp: Date.now() }]);

    const timer = setTimeout(() => {
      setToasts((prev) => prev.filter((t) => t.id !== id));
      timersRef.current.delete(id);
    }, 5000);

    timersRef.current.set(id, timer);
  }, []);

  const dismissToast = useCallback((id: number) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
    const timer = timersRef.current.get(id);
    if (timer) {
      clearTimeout(timer);
      timersRef.current.delete(id);
    }
  }, []);

  useEffect(() => {
    const timers = timersRef.current;
    return () => {
      timers.forEach((timer) => clearTimeout(timer));
    };
  }, []);

  return { toasts, addToast, dismissToast };
}

interface ToastContainerProps {
  toasts: Toast[];
  onDismiss: (id: number) => void;
}

export function ToastContainer({ toasts, onDismiss }: ToastContainerProps) {
  if (toasts.length === 0) return null;

  return (
    <div className="toast-container">
      {toasts.map((toast) => (
        <div key={toast.id} className="toast">
          <div className="toast-icon">
            <Bell size={14} />
          </div>
          <div className="toast-body">
            <div className="toast-title">
              {toast.entries.length === 1
                ? 'New entry received'
                : `${toast.entries.length} new entries received`}
            </div>
            <div className="toast-message">
              {toast.entries.map((e) => e.title).join(', ')}
            </div>
          </div>
          <button className="toast-close" onClick={() => onDismiss(toast.id)}>
            <X size={14} />
          </button>
        </div>
      ))}
    </div>
  );
}
