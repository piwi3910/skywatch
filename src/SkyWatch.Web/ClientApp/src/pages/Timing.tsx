import { useState, useEffect, useCallback } from 'react';
import {
  Box, Typography, Card, CardContent, Grid, TextField, Button,
  Switch, FormControlLabel, Snackbar, Alert, CircularProgress, Stack,
} from '@mui/material';
import { api } from '../services/api';
import type { TimingSettings } from '../services/types';

const defaultSettings: TimingSettings = {
  warmupSeconds: 30,
  unsafeDebounceSeconds: 5,
  safeRecoveryDebounceSeconds: 30,
  minimumUnsafeHoldSeconds: 60,
  failSafeOnCameraLoss: true,
  failSafeOnStaleFrame: true,
};

export default function Timing() {
  const [settings, setSettings] = useState<TimingSettings>(defaultSettings);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [snackbar, setSnackbar] = useState<{ open: boolean; message: string; severity: 'success' | 'error' }>({
    open: false, message: '', severity: 'success',
  });

  const loadSettings = useCallback(async () => {
    try {
      setLoading(true);
      const data = await api.getTiming();
      setSettings(data);
    } catch (err) {
      setSnackbar({ open: true, message: `Failed to load timing settings: ${err instanceof Error ? err.message : String(err)}`, severity: 'error' });
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadSettings();
  }, [loadSettings]);

  const handleSave = async () => {
    try {
      setSaving(true);
      const updated = await api.updateTiming(settings);
      setSettings(updated);
      setSnackbar({ open: true, message: 'Timing settings saved successfully.', severity: 'success' });
    } catch (err) {
      setSnackbar({ open: true, message: `Failed to save timing settings: ${err instanceof Error ? err.message : String(err)}`, severity: 'error' });
    } finally {
      setSaving(false);
    }
  };

  const updateNumber = (field: keyof TimingSettings, value: string) => {
    const num = Number(value);
    if (!Number.isNaN(num)) {
      setSettings((prev) => ({ ...prev, [field]: num }));
    }
  };

  const updateBool = (field: keyof TimingSettings, value: boolean) => {
    setSettings((prev) => ({ ...prev, [field]: value }));
  };

  if (loading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}>
        <CircularProgress />
      </Box>
    );
  }

  return (
    <Box>
      <Typography variant="h4" gutterBottom>Timing</Typography>

      <Stack spacing={3}>
        {/* Timing Durations */}
        <Card>
          <CardContent>
            <Typography variant="h6" gutterBottom>Timing Durations</Typography>
            <Grid container spacing={2}>
              <Grid size={{ xs: 12, sm: 6 }}>
                <TextField
                  fullWidth
                  label="Warmup Seconds"
                  type="number"
                  value={settings.warmupSeconds}
                  onChange={(e) => updateNumber('warmupSeconds', e.target.value)}
                  helperText="Time to wait after startup before evaluating safety."
                  slotProps={{ htmlInput: { min: 0 } }}
                />
              </Grid>
              <Grid size={{ xs: 12, sm: 6 }}>
                <TextField
                  fullWidth
                  label="Unsafe Debounce Seconds"
                  type="number"
                  value={settings.unsafeDebounceSeconds}
                  onChange={(e) => updateNumber('unsafeDebounceSeconds', e.target.value)}
                  helperText="Consecutive unsafe readings required before transitioning to unsafe."
                  slotProps={{ htmlInput: { min: 0 } }}
                />
              </Grid>
              <Grid size={{ xs: 12, sm: 6 }}>
                <TextField
                  fullWidth
                  label="Safe Recovery Debounce Seconds"
                  type="number"
                  value={settings.safeRecoveryDebounceSeconds}
                  onChange={(e) => updateNumber('safeRecoveryDebounceSeconds', e.target.value)}
                  helperText="Consecutive safe readings required before transitioning back to safe."
                  slotProps={{ htmlInput: { min: 0 } }}
                />
              </Grid>
              <Grid size={{ xs: 12, sm: 6 }}>
                <TextField
                  fullWidth
                  label="Minimum Unsafe Hold Seconds"
                  type="number"
                  value={settings.minimumUnsafeHoldSeconds}
                  onChange={(e) => updateNumber('minimumUnsafeHoldSeconds', e.target.value)}
                  helperText="Minimum time to remain in unsafe state before allowing safe transition."
                  slotProps={{ htmlInput: { min: 0 } }}
                />
              </Grid>
            </Grid>
          </CardContent>
        </Card>

        {/* Fail-Safe Toggles */}
        <Card>
          <CardContent>
            <Typography variant="h6" gutterBottom>Fail-Safe Behavior</Typography>
            <Stack spacing={1}>
              <FormControlLabel
                control={
                  <Switch
                    checked={settings.failSafeOnCameraLoss}
                    onChange={(e) => updateBool('failSafeOnCameraLoss', e.target.checked)}
                  />
                }
                label="Fail Safe on Camera Loss"
              />
              <Typography variant="body2" color="text.secondary" sx={{ ml: 6, mt: -0.5 }}>
                Report unsafe state when the camera connection is lost.
              </Typography>

              <FormControlLabel
                control={
                  <Switch
                    checked={settings.failSafeOnStaleFrame}
                    onChange={(e) => updateBool('failSafeOnStaleFrame', e.target.checked)}
                  />
                }
                label="Fail Safe on Stale Frame"
              />
              <Typography variant="body2" color="text.secondary" sx={{ ml: 6, mt: -0.5 }}>
                Report unsafe state when frames stop arriving within the stale timeout.
              </Typography>
            </Stack>
          </CardContent>
        </Card>

        {/* Save button */}
        <Box>
          <Button
            variant="contained"
            size="large"
            onClick={handleSave}
            disabled={saving}
          >
            {saving ? 'Saving...' : 'Save'}
          </Button>
        </Box>
      </Stack>

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
