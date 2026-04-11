import { useEffect, useRef, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import type { Entry } from '../types';

interface SignalRCallbacks {
  onEntriesAdded?: (entries: Entry[]) => void;
  onEntryUpdated?: (entry: Entry) => void;
  onEntryArchived?: (entry: Entry) => void;
  onEntryDeleted?: (entryId: string) => void;
  onReconnected?: () => void;
}

export function useSignalR(callbacks: SignalRCallbacks) {
  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const [isConnected, setIsConnected] = useState(false);

  // Store callbacks in ref to avoid reconnection on callback changes
  const callbacksRef = useRef(callbacks);
  callbacksRef.current = callbacks;

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/entries')
      .withAutomaticReconnect()
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

    connection.onreconnecting(() => setIsConnected(false));
    connection.onreconnected(() => {
      setIsConnected(true);
      // Refresh data after reconnection since we may have missed events
      callbacksRef.current.onReconnected?.();
    });
    connection.onclose(() => setIsConnected(false));

    connection
      .start()
      .then(() => setIsConnected(true))
      .catch((err) => console.error('SignalR connection failed:', err));

    return () => {
      connection.stop();
    };
  }, []);

  return { isConnected };
}
