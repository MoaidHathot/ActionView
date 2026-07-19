import { useCallback, useEffect, useMemo, useState } from 'react';
import { Info, Check, AlertTriangle } from 'lucide-react';
import type { ActionParameter, EntryAction } from '../types';
import { ActionParameterForm } from './ActionParameterForm';
import { commandPreview, commandKind } from '../utils/commandPreview';
import type { OutcomeMarker } from '../utils/markers';

interface Props {
  action: EntryAction;
  /**
   * Called when the user submits. Receives the validated parameter values, or
   * undefined when the action declares no parameters.
   */
  onClick: (parameters?: Record<string, string>) => Promise<void> | void;
  loading?: boolean;
  /**
   * Stable identity for localStorage draft persistence (so an unrelated re-render
   * caused by a SignalR update doesn't wipe a half-typed PR comment). Typically:
   *   `${entryId}.${actionIndex}` for entry actions
   *   `${entryId}.b${blockPath}.${actionIndex}` for section actions
   */
  draftKey?: string;
  /** Last recorded outcome for this action (drives the ✓/✕ result chip). */
  marker?: OutcomeMarker;
  /** When set, the button is inert and shows the reason on hover (e.g. no handler wired). */
  disabled?: boolean;
  disabledReason?: string;
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

/** Build the initial values dict from declared defaults. */
function defaultsFor(parameters: ActionParameter[] | undefined): Record<string, string> {
  if (!parameters) return {};
  const out: Record<string, string> = {};
  for (const p of parameters) out[p.name] = p.default ?? (p.type === 'boolean' ? 'false' : '');
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

export function ActionButton({ action, onClick, loading, draftKey, marker, disabled, disabledReason }: Props) {
  const hasParameters = (action.parameters?.length ?? 0) > 0;
  const storage = useMemo(() => makeDraftStorage(draftKey), [draftKey]);

  const [open, setOpen] = useState(false);            // parameter form expanded
  const [confirming, setConfirming] = useState(false); // simple confirm prompt (no params)
  const [isLoading, setIsLoading] = useState(false);
  const [showCmd, setShowCmd] = useState(false);       // command preview disclosure
  const [values, setValues] = useState<Record<string, string>>(() => ({
    ...defaultsFor(action.parameters),
    ...(storage.load() ?? {}),
  }));
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitError, setSubmitError] = useState<string | null>(null);

  // Persist drafts as the user types so a SignalR refresh / unrelated re-render
  // can't wipe a long edit.
  useEffect(() => {
    if (open && hasParameters) storage.save(values);
  }, [open, hasParameters, values, storage]);

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
    setIsLoading(true);
    setSubmitError(null);
    try {
      await onClick(parameters);
      // On success, clear draft and collapse.
      if (hasParameters) storage.clear();
      setOpen(false);
      setConfirming(false);
      setValues({ ...defaultsFor(action.parameters) });
    } catch (err) {
      setSubmitError(String(err));
    } finally {
      setIsLoading(false);
    }
  }, [onClick, hasParameters, storage, action.parameters]);

  const handlePrimaryClick = useCallback(() => {
    if (hasParameters) {
      // First click: expand the form. (Defaults are already loaded.)
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
          disabled={isLoading}
        />
        {submitError && <div className="action-param-error">{submitError}</div>}
        <div className="action-param-actions">
          <button
            className={styleClass}
            onClick={handleFormSubmit}
            disabled={isLoading || loading}
          >
            {isLoading ? 'Executing...' : action.label}
          </button>
          <button
            className="action-btn action-default"
            onClick={handleCancel}
            disabled={isLoading}
          >
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
        <button className="action-btn action-danger" onClick={handlePrimaryClick} disabled={isLoading}>
          {isLoading ? 'Executing...' : 'Confirm'}
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
          <button
            className={styleClass}
            onClick={handlePrimaryClick}
            disabled={isLoading || loading}
          >
            {isLoading ? 'Executing...' : action.label}
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
      {disabled && disabledReason && (
        <div className="action-btn-hint">{disabledReason}</div>
      )}
    </div>
  );
}

/** Compact absolute-ish time for chips/tooltips (local time, HH:MM). */
function formatWhen(iso: string): string {
  try {
    const d = new Date(iso);
    return d.toLocaleString(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
  } catch {
    return iso;
  }
}
