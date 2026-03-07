import { Alert, AlertTitle } from '@mui/material';
import type { StatusData } from '../services/types';

interface StatusBannerProps {
  status: StatusData | null;
  connected: boolean;
}

function getSeverity(status: StatusData | null, connected: boolean): 'success' | 'error' | 'warning' | 'info' {
  if (!connected) return 'warning';
  if (!status) return 'info';
  if (status.isSafe) return 'success';
  if (status.safetyState === 'Unsafe') return 'error';
  return 'warning';
}

function getTitle(status: StatusData | null, connected: boolean): string {
  if (!connected) return 'Disconnected';
  if (!status) return 'Waiting for data...';
  return status.safetyState;
}

export default function StatusBanner({ status, connected }: StatusBannerProps) {
  const severity = getSeverity(status, connected);
  const title = getTitle(status, connected);

  return (
    <Alert severity={severity} variant="filled" sx={{ mb: 2 }}>
      <AlertTitle sx={{ fontWeight: 'bold', fontSize: '1.1rem' }}>{title}</AlertTitle>
      {!connected && 'Connection to SkyWatch server lost. Attempting to reconnect...'}
      {connected && !status && 'Waiting for initial status data from the server.'}
      {connected && status && status.explanation}
    </Alert>
  );
}
