import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Info, Check, AlertTriangle, Loader2, Square } from 'lucide-react';
import type { ActionParameter, EntryAction, ActionJob } from '../types';
import { ActionParameterForm } from './ActionParameterForm';
import { commandPreview, commandKind } from '../utils/commandPreview';
import type { OutcomeMarker } from '../utils/markers';
import { useActionJob, useActionJobs } from '../context/ActionJobsProvider';

interface Props {
  action: EntryAction;
  /**
   * Called when the user submits. Receives validated parameter values (or
   * undefined). Returns the started ActionJob so the button can track progress.
   */
  onClick: (parameters?: Record<string, string>) => Promise<ActionJob | void> | ActionJob | void;
  loading?: boolean;
  /** Stable identity for localStorage draft persistence. */
  draftKey?: string;
  /** Last recorded outcome for this action (drives the ✓/✕ result chip). */
  marker?: OutcomeMarker;
  /** When set, the button is inert and shows the reason on hover (e.g. no handler wired). */
  disabled?: boolean;
  disabledReason?: string;
  /** Expands {{content.*}}/{{entry.*}} references in the parameter defaults for display. */
  expandDefault?: (text: string) => string;
}

interface DraftStorage {
  load(): Record<string, string> | null;
  save(values: Record<string, string>): void;
  clear(): void;
}

function makeDraftStorage(key: string | undefined): DraftStorage {
  const fullKey = key ? `actionview.draft.${key}` : null;
  return {
    load: () => {
      if (!fullKey) return null;
      try {
        const raw = window.localStorage.getItem(fullKey);
        return raw ? JSON.parse(raw) as Record<string, string> : null;
      } catch {
        return null;
      }
    },
    save: (values) => {
      if (!fullKey) return;
      try { window.localStorage.setItem(fullKey, JSON.stringify(values)); } catch { /* quota / private mode */ }
    },
    clear: () => {
      if (!fullKey) return;
      try { window.localStorage.removeItem(fullKey); } catch { /* noop */ }
    },
  };
}

/** Build the initial values dict from declared defaults (expanding content refs for display). */
function defaultsFor(parameters: ActionParameter[] | undefined, expand?: (t: string) => string): Record<string, string> {
  if (!parameters) return {};
  const out: Record<string, string> = {};
  for (const p of parameters) {
    const raw = p.default ?? (p.type === 'boolean' ? 'false' : '');
    out[p.name] = expand && raw ? expand(raw) : raw;
  }
  return out;
}

/** Mirror of server-side ActionParameterValidator (best-effort, server is still authoritative). */
function validate(parameters: ActionParameter[] | undefined, values: Record<string, string>): Record<string, string> {
  const errors: Record<string, string> = {};
  if (!parameters) return errors;
  for (const p of parameters) {
    const v = (values[p.name] ?? '').trim();
    if (p.required && v === '') {
      errors[p.name] = `${p.label} is required.`;
      continue;
    }
    if (v === '') continue;
    if (p.type === 'number' && Number.isNaN(Number(v))) {
      errors[p.name] = `${p.label} must be a number.`;
    } else if (p.type === 'select' && p.options && !p.options.includes(v)) {
      errors[p.name] = `${p.label} must be one of: ${p.options.join(', ')}.`;
    }
  }
  return errors;
}

export function ActionButton({ action, onClick, loading, draftKey, marker, disabled, disabledReason, expandDefault }: Props) {
  const hasParameters = (action.parameters?.length ?? 0) > 0;
  const storage = useMemo(() => makeDraftStorage(draftKey), [draftKey]);

  const [open, setOpen] = useState(false);            // parameter form expanded
  const [confirming, setConfirming] = useState(false); // simple confirm prompt (no params)
  const [showCmd, setShowCmd] = useState(false);       // command preview disclosure
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [jobId, setJobId] = useState<string | undefined>(undefined);
  const [values, setValues] = useState<Record<string, string>>(() => ({
    ...defaultsFor(action.parameters, expandDefault),
    ...(storage.load() ?? {}),
  }));
  const [errors, setErrors] = useState<Record<string, string>>({});

  const { cancel } = useActionJobs();
  const job = useActionJob(jobId);
  const running = !!job && (job.status === 'pending' || job.status === 'running');

  // Persist drafts as the user types.
  useEffect(() => {
    if (open && hasParameters) storage.save(values);
  }, [open, hasParameters, values, storage]);

  // React once to a job reaching a terminal state.
  const prevStatus = useRef<string | undefined>(undefined);
  useEffect(() => {
    if (!job) return;
    if (job.status === prevStatus.current) return;
    prevStatus.current = job.status;
    if (job.status === 'succeeded') {
      if (hasParameters) storage.clear();
      setOpen(false);
      setConfirming(false);
      setSubmitError(null);
      setValues({ ...defaultsFor(action.parameters, expandDefault) });
    } else if (job.status === 'failed' || job.status === 'cancelled') {
      setSubmitError(job.message ?? job.status);
    }
  }, [job, hasParameters, storage, action.parameters, expandDefault]);

  const styleClass = `action-btn action-${action.style}`;

  const handleParamChange = useCallback((name: string, value: string) => {
    setValues((prev) => ({ ...prev, [name]: value }));
    setErrors((prev) => {
      if (!prev[name]) return prev;
      const next = { ...prev };
      delete next[name];
      return next;
    });
  }, []);

  const submit = useCallback(async (parameters?: Record<string, string>) => {
    setSubmitError(null);
    prevStatus.current = undefined;
    try {
      const started = await onClick(parameters);
      if (started && typeof started === 'object' && 'id' in started) {
        setJobId(started.id);
      } else {
        // No job returned (e.g. a non-job handler): treat as immediately done.
        if (hasParameters) storage.clear();
        setOpen(false);
        setConfirming(false);
      }
    } catch (err) {
      setSubmitError(String(err));
    }
  }, [onClick, hasParameters, storage]);

  const handlePrimaryClick = useCallback(() => {
    if (hasParameters) {
      if (!open) setOpen(true);
      return;
    }
    if (action.confirmMessage && !confirming) {
      setConfirming(true);
      return;
    }
    setConfirming(false);
    void submit(undefined);
  }, [hasParameters, open, action.confirmMessage, confirming, submit]);

  const handleFormSubmit = useCallback(() => {
    const validationErrors = validate(action.parameters, values);
    if (Object.keys(validationErrors).length > 0) {
      setErrors(validationErrors);
      return;
    }
    void submit(values);
  }, [action.parameters, values, submit]);

  const handleCancel = useCallback(() => {
    setOpen(false);
    setConfirming(false);
    setErrors({});
    setSubmitError(null);
  }, []);

  // --- Running (job in flight): show progress + cancel, replaces the button ---
  if (running && job) {
    return <RunningAction job={job} onCancel={() => cancel(job.id)} />;
  }

  // --- Parameter form mode ---
  if (open && hasParameters) {
    return (
      <div className="action-param-container">
        <div className="action-param-header">
          <span className="action-param-title">{action.label}</span>
        </div>
        {action.confirmMessage && (
          <div className="action-param-confirm-message">{action.confirmMessage}</div>
        )}
        <ActionParameterForm
          parameters={action.parameters!}
          values={values}
          onChange={handleParamChange}
          errors={errors}
        />
        {submitError && <div className="action-param-error">{submitError}</div>}
        <div className="action-param-actions">
          <button className={styleClass} onClick={handleFormSubmit}>
            {action.label}
          </button>
          <button className="action-btn action-default" onClick={handleCancel}>
            Cancel
          </button>
        </div>
      </div>
    );
  }

  // --- Confirm prompt (no parameters) ---
  if (confirming) {
    return (
      <div className="action-confirm">
        <span className="confirm-message">{action.confirmMessage}</span>
        <button className="action-btn action-danger" onClick={handlePrimaryClick}>
          Confirm
        </button>
        <button className="action-btn action-default" onClick={handleCancel}>
          Cancel
        </button>
      </div>
    );
  }

  // --- Default button (with command preview + last-outcome chip) ---
  const preview = commandPreview(action.command);
  return (
    <div className="action-btn-wrap">
      <div className="action-btn-row">
        {disabled ? (
          <button className={`${styleClass} action-btn-disabled`} disabled title={disabledReason}>
            {action.label}
          </button>
        ) : (
          <button className={styleClass} onClick={handlePrimaryClick} disabled={loading}>
            {action.label}
          </button>
        )}
        {preview && (
          <button
            type="button"
            className={`action-cmd-toggle ${showCmd ? 'active' : ''}`}
            onClick={() => setShowCmd((v) => !v)}
            title="What does this run?"
            aria-label="Show command"
            aria-expanded={showCmd}
          >
            <Info size={13} />
          </button>
        )}
        {marker && (
          <span
            className={`action-marker ${marker.success ? 'action-marker-success' : 'action-marker-fail'}`}
            title={`${marker.label} — ${marker.success ? 'succeeded' : 'failed'} ${formatWhen(marker.timestamp)}`}
          >
            {marker.success ? <Check size={12} /> : <AlertTriangle size={12} />}
            {marker.success ? marker.label : `${marker.label} failed`}
          </span>
        )}
      </div>
      {showCmd && preview && (
        <div className="action-cmd-preview">
          <span className="action-cmd-kind">{commandKind(action.command)}</span>
          <code>{preview}</code>
        </div>
      )}
      {submitError && <div className="action-btn-hint action-btn-error">{submitError}</div>}
      {disabled && disabledReason && (
        <div className="action-btn-hint">{disabledReason}</div>
      )}
    </div>
  );
}

/** Live running state for an in-flight action job: spinner, elapsed timer, output tail, cancel. */
function RunningAction({ job, onCancel }: { job: ActionJob; onCancel: () => void }) {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const t = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(t);
  }, []);
  const elapsed = Math.max(0, Math.floor((now - new Date(job.startedAt).getTime()) / 1000));
  const tail = job.outputTail?.slice(-3) ?? [];
  return (
    <div className="action-running">
      <div className="action-running-head">
        <Loader2 size={14} className="spin" />
        <span className="action-running-label">{job.actionLabel}</span>
        <span className="action-running-status">{job.status === 'pending' ? 'queued' : 'running'}</span>
        <span className="action-running-timer">{formatElapsed(elapsed)}</span>
        <button className="action-cancel-btn" onClick={onCancel} title="Cancel">
          <Square size={12} /> Cancel
        </button>
      </div>
      {tail.length > 0 && (
        <pre className="action-running-output">{tail.join('\n')}</pre>
      )}
    </div>
  );
}

function formatElapsed(sec: number): string {
  if (sec < 60) return `${sec}s`;
  const m = Math.floor(sec / 60);
  const s = sec % 60;
  return `${m}m ${s.toString().padStart(2, '0')}s`;
}

/** Compact absolute-ish time for chips/tooltips (local time). */
function formatWhen(iso: string): string {
  try {
    const d = new Date(iso);
    return d.toLocaleString(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
  } catch {
    return iso;
  }
}
