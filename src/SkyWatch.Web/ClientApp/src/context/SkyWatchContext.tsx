import { createContext, useContext, type ReactNode } from 'react';
import { useWebSocket } from '../hooks/useWebSocket';
import type { StatusData } from '../services/types';

interface SkyWatchContextValue {
  status: StatusData | null;
  previewUrl: string | null;
  connected: boolean;
}

const SkyWatchContext = createContext<SkyWatchContextValue>({
  status: null,
  previewUrl: null,
  connected: false,
});

export function SkyWatchProvider({ children }: { children: ReactNode }) {
  const value = useWebSocket('/ws/live');
  return (
    <SkyWatchContext.Provider value={value}>
      {children}
    </SkyWatchContext.Provider>
  );
}

export function useSkyWatch() {
  return useContext(SkyWatchContext);
}
