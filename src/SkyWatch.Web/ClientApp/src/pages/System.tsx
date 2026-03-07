import { useState, useEffect, useCallback } from 'react';
import {
  Box, Typography, Card, CardContent, Grid, ToggleButtonGroup,
  ToggleButton, Snackbar, Alert, Chip, Divider, Stack,
  CircularProgress,
} from '@mui/material';
import { api } from '../services/api';
import { useSkyWatch } from '../context/SkyWatchContext';

export default function System() {
  const { status, connected } = useSkyWatch();
  const [overrideMode, setOverrideMode] = useState<string>('Automatic');
  const [loadingOverride, setLoadingOverride] = useState(true);
  const [snackbar, setSnackbar] = useState<{ open: boolean; message: string; severity: 'success' | 'error' }>({
    open: false, message: '', severity: 'success',
  });

  const showMessage = (message: string, severity: 'success' | 'error') => {
    setSnackbar({ open: true, message, severity });
  };

  const loadOverride = useCallback(async () => {
    try {
      setLoadingOverride(true);
      const data = await api.getOverride();
      setOverrideMode(data.mode);
    } catch (err) {
      showMessage(`Failed to load override: ${err instanceof Error ? err.message : String(err)}`, 'error');
    } finally {
      setLoadingOverride(false);
    }
  }, []);

  useEffect(() => {
    void loadOverride();
  }, [loadOverride]);

  const handleOverrideChange = async (_event: React.MouseEvent<HTMLElement>, newMode: string | null) => {
    if (newMode === null) return;
    try {
      await api.updateOverride(newMode);
      setOverrideMode(newMode);
      showMessage(`Override set to "${newMode}".`, 'success');
    } catch (err) {
      showMessage(`Failed to update override: ${err instanceof Error ? err.message : String(err)}`, 'error');
    }
  };

  const capture = status?.capture;

  return (
    <Box>
      <Typography variant="h4" gutterBottom>System</Typography>

      <Grid container spacing={3}>
        {/* Manual Override */}
        <Grid size={{ xs: 12, md: 6 }}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom>Manual Override</Typography>
              <Divider sx={{ mb: 2 }} />
              {loadingOverride ? (
                <Box sx={{ display: 'flex', justifyContent: 'center', py: 2 }}>
                  <CircularProgress size={24} />
                </Box>
              ) : (
                <ToggleButtonGroup
                  value={overrideMode}
                  exclusive
                  onChange={(_e, v) => void handleOverrideChange(_e, v)}
                  fullWidth
                >
                  <ToggleButton value="Automatic" color="standard">
                    Automatic
                  </ToggleButton>
                  <ToggleButton value="ForceSafe" color="success">
                    Force Safe
                  </ToggleButton>
                  <ToggleButton value="ForceUnsafe" color="error">
                    Force Unsafe
                  </ToggleButton>
                </ToggleButtonGroup>
              )}
            </CardContent>
          </Card>
        </Grid>

        {/* System Info */}
        <Grid size={{ xs: 12, md: 6 }}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom>System Info</Typography>
              <Divider sx={{ mb: 2 }} />
              <Stack spacing={1.5}>
                <InfoRow label="Safety State" value={status?.safetyState ?? 'Unknown'} />
                <InfoRow
                  label="Connection"
                  value={
                    <Chip
                      label={connected ? 'Connected' : 'Disconnected'}
                      color={connected ? 'success' : 'error'}
                      size="small"
                      variant="outlined"
                    />
                  }
                />
                <InfoRow label="Source Type" value={capture?.sourceType ?? 'N/A'} />
                <InfoRow label="Source Description" value={capture?.sourceDescription ?? 'N/A'} />
                <InfoRow label="Frames Captured" value={String(capture?.framesCaptured ?? 0)} />
              </Stack>
            </CardContent>
          </Card>
        </Grid>

        {/* Capture Status */}
        <Grid size={{ xs: 12, md: 6 }}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom>Capture Status</Typography>
              <Divider sx={{ mb: 2 }} />
              <Stack spacing={1.5}>
                <InfoRow
                  label="Connected"
                  value={
                    <Chip
                      label={capture?.isConnected ? 'Yes' : 'No'}
                      color={capture?.isConnected ? 'success' : 'error'}
                      size="small"
                      variant="outlined"
                    />
                  }
                />
                <InfoRow label="Source Type" value={capture?.sourceType ?? 'N/A'} />
                <InfoRow label="Description" value={capture?.sourceDescription ?? 'N/A'} />
                <InfoRow label="Frames Captured" value={String(capture?.framesCaptured ?? 0)} />
                {capture?.errorMessage && (
                  <Alert severity="error" sx={{ mt: 1 }}>
                    {capture.errorMessage}
                  </Alert>
                )}
              </Stack>
            </CardContent>
          </Card>
        </Grid>

        {/* About */}
        <Grid size={{ xs: 12, md: 6 }}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom>About</Typography>
              <Divider sx={{ mb: 2 }} />
              <Stack spacing={1.5}>
                <InfoRow label="Application" value="SkyWatch" />
                <InfoRow label="Version" value="1.0.0" />
                <InfoRow label="Framework" value=".NET 8 / React" />
                <InfoRow label="Protocol" value="ASCOM Alpaca" />
              </Stack>
            </CardContent>
          </Card>
        </Grid>
      </Grid>

      <Snackbar
        open={snackbar.open}
        autoHideDuration={4000}
        onClose={() => setSnackbar((s) => ({ ...s, open: false }))}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert
          onClose={() => setSnackbar((s) => ({ ...s, open: false }))}
          severity={snackbar.severity}
          variant="filled"
        >
          {snackbar.message}
        </Alert>
      </Snackbar>
    </Box>
  );
}

function InfoRow({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <Stack direction="row" justifyContent="space-between" alignItems="center">
      <Typography variant="body2" color="text.secondary">
        {label}
      </Typography>
      {typeof value === 'string' ? (
        <Typography variant="body2" fontWeight="medium">
          {value}
        </Typography>
      ) : (
        value
      )}
    </Stack>
  );
}
