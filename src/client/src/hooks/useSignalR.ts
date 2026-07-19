import { useEffect, useRef, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import type { Entry, ActionJob } from '../types';

interface SignalRCallbacks {
  onEntriesAdded?: (entries: Entry[]) => void;
  onEntryUpdated?: (entry: Entry) => void;
  onEntryArchived?: (entry: Entry) => void;
  onEntryDeleted?: (entryId: string) => void;
  onConfigChanged?: () => void;
  onActionJobStarted?: (job: ActionJob) => void;
  onActionJobProgress?: (jobId: string, line: string) => void;
  onActionJobFinished?: (job: ActionJob) => void;
  onReconnected?: () => void;
}

const reconnectPolicy: signalR.IRetryPolicy = {
  nextRetryDelayInMilliseconds: (retryContext) => {
    const delays = [0, 2_000, 10_000, 30_000, 60_000];
    return delays[Math.min(retryContext.previousRetryCount, delays.length - 1)];
  },
};

export function useSignalR(callbacks: SignalRCallbacks) {
  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const [isConnected, setIsConnected] = useState(false);

  // Store callbacks in ref to avoid reconnection on callback changes
  const callbacksRef = useRef(callbacks);
  callbacksRef.current = callbacks;

  useEffect(() => {
    let disposed = false;
    let startRetryTimer: number | undefined;
    let resyncAfterStart = false;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/entries')
      .withAutomaticReconnect(reconnectPolicy)
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    connectionRef.current = connection;

    connection.on('EntriesAdded', (entries: Entry[]) => {
      callbacksRef.current.onEntriesAdded?.(entries);
    });

    connection.on('EntryUpdated', (entry: Entry) => {
      callbacksRef.current.onEntryUpdated?.(entry);
    });

    connection.on('EntryArchived', (entry: Entry) => {
      callbacksRef.current.onEntryArchived?.(entry);
    });

    connection.on('EntryDeleted', (entryId: string) => {
      callbacksRef.current.onEntryDeleted?.(entryId);
    });

    connection.on('ConfigChanged', () => {
      callbacksRef.current.onConfigChanged?.();
    });

    connection.on('ActionJobStarted', (job: ActionJob) => {
      callbacksRef.current.onActionJobStarted?.(job);
    });

    connection.on('ActionJobProgress', (jobId: string, line: string) => {
      callbacksRef.current.onActionJobProgress?.(jobId, line);
    });

    connection.on('ActionJobFinished', (job: ActionJob) => {
      callbacksRef.current.onActionJobFinished?.(job);
    });

    connection.onreconnected(() => {
      setIsConnected(true);
      callbacksRef.current.onReconnected?.();
    });

    connection.onreconnecting(() => setIsConnected(false));
    connection.onclose(() => {
      setIsConnected(false);
      if (!disposed) {
        resyncAfterStart = true;
        scheduleStartRetry(60_000);
      }
    });

    function scheduleStartRetry(delayMs: number) {
      window.clearTimeout(startRetryTimer);
      startRetryTimer = window.setTimeout(startConnection, delayMs);
    }

    async function startConnection() {
      if (disposed || connection.state !== signalR.HubConnectionState.Disconnected) {
        return;
      }

      try {
        await connection.start();
        if (!disposed) {
          setIsConnected(true);
          if (resyncAfterStart) {
            resyncAfterStart = false;
            callbacksRef.current.onReconnected?.();
          }
        }
      } catch (err) {
        if (disposed) return;
        resyncAfterStart = true;
        setIsConnected(false);
        console.error('SignalR connection failed; retrying in 60 seconds:', err);
        scheduleStartRetry(60_000);
      }
    }

    void startConnection();

    return () => {
      disposed = true;
      window.clearTimeout(startRetryTimer);
      void connection.stop();
    };
  }, []);

  return { isConnected };
}
