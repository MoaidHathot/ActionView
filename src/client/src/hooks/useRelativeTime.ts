import { useState, useEffect } from 'react';
import { formatDistanceToNow } from '../utils/time';

/**
 * Returns a relative time string that auto-refreshes.
 * Updates every 30 seconds for recent times, every minute for older ones.
 */
export function useRelativeTime(dateStr: string | undefined): string {
  const [, setTick] = useState(0);

  useEffect(() => {
    if (!dateStr) return;
    const interval = setInterval(() => {
      setTick((t) => t + 1);
    }, 30_000);
    return () => clearInterval(interval);
  }, [dateStr]);

  if (!dateStr) return '';
  return formatDistanceToNow(dateStr);
}

/**
 * Triggers a re-render on a global interval so all timestamps update together.
 * Call once at the app level; child components using formatDistanceToNow will
 * get fresh values on re-render.
 */
export function useTimestampRefresh(intervalMs = 30_000) {
  const [tick, setTick] = useState(0);

  useEffect(() => {
    const interval = setInterval(() => {
      setTick((t) => t + 1);
    }, intervalMs);
    return () => clearInterval(interval);
  }, [intervalMs]);

  return tick;
}
