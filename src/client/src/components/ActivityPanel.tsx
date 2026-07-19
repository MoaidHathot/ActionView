import { Check, AlertTriangle, Terminal, Globe, ChevronDown } from 'lucide-react';
import { useState } from 'react';
import type { ActionEvent } from '../types';
import { commandPreview } from '../utils/commandPreview';

interface Props {
  events: ActionEvent[];
  loading?: boolean;
}

/**
 * Renders the append-only action history for an entry (newest first). Sourced
 * from GET /api/entries/{id}/history, which survives archive/dismiss/delete, so
 * you can review everything that ran against an entry — including section
 * approvals and dismiss/delete lifecycle events that were previously invisible.
 */
export function ActivityPanel({ events, loading }: Props) {
  if (loading) return <div className="activity-empty">Loading activity…</div>;
  if (events.length === 0) {
    return <div className="activity-empty">No recorded activity for this entry yet.</div>;
  }
  return (
    <ul className="activity-list">
      {events.map((ev) => (
        <ActivityRow key={ev.id} ev={ev} />
      ))}
    </ul>
  );
}

function ActivityRow({ ev }: { ev: ActionEvent }) {
  const [open, setOpen] = useState(false);
  const preview = ev.command ? commandPreview(ev.command) : '';
  const hasDetail = Boolean(preview || ev.output || ev.message);
  return (
    <li className={`activity-row ${ev.success ? 'activity-ok' : 'activity-fail'}`}>
      <div className="activity-row-head">
        <span className="activity-status" aria-hidden>
          {ev.success ? <Check size={14} /> : <AlertTriangle size={14} />}
        </span>
        <span className={`activity-label action-${ev.actionStyle}`}>{ev.actionLabel}</span>
        <span className="activity-target">{targetText(ev)}</span>
        <span className="activity-time">{formatWhen(ev.timestamp)}</span>
        {hasDetail && (
          <button
            type="button"
            className={`activity-expand ${open ? 'active' : ''}`}
            onClick={() => setOpen((v) => !v)}
            aria-label="Toggle details"
          >
            <ChevronDown size={14} />
          </button>
        )}
      </div>
      {open && hasDetail && (
        <div className="activity-detail">
          {preview && (
            <div className="activity-cmd">
              {ev.command?.type === 'cli' ? <Terminal size={12} /> : <Globe size={12} />}
              <code>{preview}</code>
            </div>
          )}
          {ev.message && <div className="activity-message">{ev.message}</div>}
          {ev.output && <pre className="activity-output">{ev.output}</pre>}
        </div>
      )}
    </li>
  );
}

function targetText(ev: ActionEvent): string {
  const trig = ev.trigger && ev.trigger !== 'click' ? ` · ${ev.trigger}` : '';
  if (ev.target === 'section') return `comment/section${trig}`;
  if (ev.target === 'system') return `entry${trig}`;
  const post = ev.postBehavior ? ` → ${ev.postBehavior}` : '';
  return `entry action${post}${trig}`;
}

function formatWhen(iso: string): string {
  try {
    return new Date(iso).toLocaleString(undefined, {
      month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit',
    });
  } catch {
    return iso;
  }
}
