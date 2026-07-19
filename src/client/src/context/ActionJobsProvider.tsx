import { createContext, useCallback, useContext, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import type { ActionJob } from '../types';
import { api } from '../api/client';

interface ActionJobsContextValue {
  jobs: Map<string, ActionJob>;
  /** Insert or replace a job (from the POST response or SignalR started/finished). */
  upsert: (job: ActionJob) => void;
  /** Append a streamed output line to a running job. */
  progress: (jobId: string, line: string) => void;
  /** Request cancellation of a running job. */
  cancel: (jobId: string) => void;
}

const ActionJobsContext = createContext<ActionJobsContextValue | null>(null);

const TAIL_LIMIT = 200;

/**
 * Holds live state for background action jobs, fed by SignalR
 * (ActionJobStarted/Progress/Finished) and by the POST that starts a job.
 * Any button can read its job by id without prop-drilling.
 */
export function ActionJobsProvider({ children }: { children: ReactNode }) {
  const [jobs, setJobs] = useState<Map<string, ActionJob>>(() => new Map());

  const upsert = useCallback((job: ActionJob) => {
    setJobs((prev) => {
      const next = new Map(prev);
      const existing = next.get(job.id);
      // Preserve any locally-accumulated output tail if the incoming snapshot lacks it.
      const outputTail = job.outputTail?.length ? job.outputTail : existing?.outputTail ?? [];
      next.set(job.id, { ...job, outputTail });
      return next;
    });
  }, []);

  const progress = useCallback((jobId: string, line: string) => {
    setJobs((prev) => {
      const existing = prev.get(jobId);
      if (!existing) return prev;
      const next = new Map(prev);
      const tail = [...(existing.outputTail ?? []), line].slice(-TAIL_LIMIT);
      next.set(jobId, { ...existing, status: 'running', outputTail: tail });
      return next;
    });
  }, []);

  const cancel = useCallback((jobId: string) => {
    void api.cancelJob(jobId).catch(() => { /* already finished */ });
  }, []);

  const value = useMemo(() => ({ jobs, upsert, progress, cancel }), [jobs, upsert, progress, cancel]);
  return <ActionJobsContext.Provider value={value}>{children}</ActionJobsContext.Provider>;
}

export function useActionJobs(): ActionJobsContextValue {
  const ctx = useContext(ActionJobsContext);
  if (!ctx) throw new Error('useActionJobs must be used within an ActionJobsProvider');
  return ctx;
}

/** Convenience: read a single job (or undefined) by id. */
export function useActionJob(jobId: string | undefined): ActionJob | undefined {
  const { jobs } = useActionJobs();
  return jobId ? jobs.get(jobId) : undefined;
}
