import { useState, useEffect, useCallback } from 'react';
import {
  Box, Typography, Card, CardContent, Grid, TextField, Button,
  FormControl, InputLabel, Select, MenuItem, Switch, FormControlLabel,
  Chip, Alert, Snackbar, CircularProgress, Stack,
} from '@mui/material';
import type { SelectChangeEvent } from '@mui/material';
import { api } from '../services/api';
import { useSkyWatch } from '../context/SkyWatchContext';
import type { CaptureSettings } from '../services/types';

const defaultSettings: CaptureSettings = {
  sourceType: 'UsbCamera',
  sourceIdentifier: '',
  deviceIndex: 0,
  frameIntervalSeconds: 5,
  targetWidth: 1280,
  targetHeight: 720,
  reconnectIntervalSeconds: 10,
  staleFrameTimeoutSeconds: 30,
  connectionTimeoutSeconds: 10,
  flipHorizontal: false,
  flipVertical: false,
  rotationDegrees: 0,
  alpacaHost: '',
  alpacaPort: 11111,
  alpacaDeviceNumber: 0,
  ascomProgId: '',
  exposureDuration: 2.0,
  autoExposure: false,
  autoExposureTarget: 128,
  exposureMin: 0.1,
  exposureMax: 30.0,
};

export default function Sources() {
  const { status } = useSkyWatch();
  const [settings, setSettings] = useState<CaptureSettings>(defaultSettings);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [snackbar, setSnackbar] = useState<{ open: boolean; message: string; severity: 'success' | 'error' }>({
    open: false, message: '', severity: 'success',
  });

  const loadSettings = useCallback(async () => {
    try {
      setLoading(true);
      const data = await api.getCaptureSettings();
      setSettings(data);
    } catch (err) {
      setSnackbar({ open: true, message: `Failed to load settings: ${err instanceof Error ? err.message : String(err)}`, severity: 'error' });
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
      const updated = await api.updateCaptureSettings(settings);
      setSettings(updated);
      setSnackbar({ open: true, message: 'Capture settings saved and source reconnected.', severity: 'success' });
    } catch (err) {
      setSnackbar({ open: true, message: `Failed to save settings: ${err instanceof Error ? err.message : String(err)}`, severity: 'error' });
    } finally {
      setSaving(false);
    }
  };

  const update = <K extends keyof CaptureSettings>(field: K, value: CaptureSettings[K]) => {
    setSettings((prev) => ({ ...prev, [field]: value }));
  };

  const handleSourceTypeChange = (e: SelectChangeEvent) => {
    update('sourceType', e.target.value as CaptureSettings['sourceType']);
  };

  const handleRotationChange = (e: SelectChangeEvent) => {
    update('rotationDegrees', Number(e.target.value));
  };

  const captureStatus = status?.capture;
  const connectionColor = captureStatus
    ? captureStatus.isConnected ? 'success' : (captureStatus.errorMessage ? 'warning' : 'error')
    : 'default';
  const connectionLabel = captureStatus
    ? captureStatus.isConnected ? 'Connected' : (captureStatus.errorMessage ? 'Error' : 'Disconnected')
    : 'Unknown';

  if (loading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}>
        <CircularProgress />
      </Box>
    );
  }

  return (
    <Box>
      <Stack direction="row" alignItems="center" spacing={2} sx={{ mb: 3 }}>
        <Typography variant="h4">Sources</Typography>
        <Chip
          label={connectionLabel}
          color={connectionColor as 'success' | 'warning' | 'error' | 'default'}
          size="small"
        />
        {captureStatus?.autoExposureEnabled && captureStatus.currentExposureDuration != null && (
          <Chip
            label={`Exposure: ${captureStatus.currentExposureDuration.toFixed(2)}s`}
            size="small"
            variant="outlined"
          />
        )}
      </Stack>

      {captureStatus?.errorMessage && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {captureStatus.errorMessage}
        </Alert>
      )}

      <Grid container spacing={3}>
        {/* Source Type Selection */}
        <Grid size={12}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom>Source Type</Typography>
              <FormControl fullWidth>
                <InputLabel id="source-type-label">Source Type</InputLabel>
                <Select
                  labelId="source-type-label"
                  value={settings.sourceType}
                  label="Source Type"
                  onChange={handleSourceTypeChange}
                >
                  <MenuItem value="UsbCamera">USB Camera</MenuItem>
                  <MenuItem value="Rtsp">RTSP Stream</MenuItem>
                  <MenuItem value="Replay">Replay</MenuItem>
                  <MenuItem value="AlpacaCamera">Alpaca Camera</MenuItem>
                  <MenuItem value="AscomCamera">ASCOM Camera (Windows)</MenuItem>
                </Select>
              </FormControl>
            </CardContent>
          </Card>
        </Grid>

        {/* Source-specific fields */}
        <Grid size={12}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom>Source Configuration</Typography>
              <Grid container spacing={2}>
                {settings.sourceType === 'UsbCamera' && (
                  <>
                    <Grid size={{ xs: 12, sm: 4 }}>
                      <TextField
                        fullWidth
                        label="Device Index"
                        type="number"
                        value={settings.deviceIndex}
                        onChange={(e) => update('deviceIndex', Number(e.target.value))}
                        slotProps={{ htmlInput: { min: 0 } }}
                      />
                    </Grid>
                    <Grid size={{ xs: 12, sm: 4 }}>
                      <TextField
                        fullWidth
                        label="Target Width"
                        type="number"
                        value={settings.targetWidth}
                        onChange={(e) => update('targetWidth', Number(e.target.value))}
                        slotProps={{ htmlInput: { min: 1 } }}
                      />
                    </Grid>
                    <Grid size={{ xs: 12, sm: 4 }}>
                      <TextField
                        fullWidth
                        label="Target Height"
                        type="number"
                        value={settings.targetHeight}
                        onChange={(e) => update('targetHeight', Number(e.target.value))}
                        slotProps={{ htmlInput: { min: 1 } }}
                      />
                    </Grid>
                  </>
                )}

                {settings.sourceType === 'Rtsp' && (
                  <>
                    <Grid size={{ xs: 12, sm: 8 }}>
                      <TextField
                        fullWidth
                        label="Source Identifier (URL)"
                        value={settings.sourceIdentifier}
                        onChange={(e) => update('sourceIdentifier', e.target.value)}
                        placeholder="rtsp://192.168.1.100:554/stream"
                      />
                    </Grid>
                    <Grid size={{ xs: 12, sm: 4 }}>
                      <TextField
                        fullWidth
                        label="Connection Timeout (s)"
                        type="number"
                        value={settings.connectionTimeoutSeconds}
                        onChange={(e) => update('connectionTimeoutSeconds', Number(e.target.value))}
                        slotProps={{ htmlInput: { min: 1 } }}
                      />
                    </Grid>
                  </>
                )}

                {settings.sourceType === 'Replay' && (
                  <>
                    <Grid size={{ xs: 12, sm: 8 }}>
                      <TextField
                        fullWidth
                        label="Source Identifier (Directory Path)"
                        value={settings.sourceIdentifier}
                        onChange={(e) => update('sourceIdentifier', e.target.value)}
                        placeholder="/path/to/image/directory"
                      />
                    </Grid>
                    <Grid size={{ xs: 12, sm: 4 }}>
                      <TextField
                        fullWidth
                        label="Frame Interval (s)"
                        type="number"
                        value={settings.frameIntervalSeconds}
                        onChange={(e) => update('frameIntervalSeconds', Number(e.target.value))}
                        slotProps={{ htmlInput: { min: 0.1, step: 0.1 } }}
                      />
                    </Grid>
                  </>
                )}

                {settings.sourceType === 'AlpacaCamera' && (
                  <>
                    <Grid size={{ xs: 12, sm: 4 }}>
                      <TextField fullWidth label="Alpaca Host" value={settings.alpacaHost}
                        onChange={(e) => update('alpacaHost', e.target.value)} placeholder="192.168.1.100" />
                    </Grid>
                    <Grid size={{ xs: 12, sm: 4 }}>
                      <TextField fullWidth label="Alpaca Port" type="number" value={settings.alpacaPort}
                        onChange={(e) => update('alpacaPort', Number(e.target.value))}
                        slotProps={{ htmlInput: { min: 1, max: 65535 } }} />
                    </Grid>
                    <Grid size={{ xs: 12, sm: 4 }}>
                      <TextField fullWidth label="Device Number" type="number" value={settings.alpacaDeviceNumber}
                        onChange={(e) => update('alpacaDeviceNumber', Number(e.target.value))}
                        slotProps={{ htmlInput: { min: 0 } }} />
                    </Grid>
                  </>
                )}

                {settings.sourceType === 'AscomCamera' && (
                  <Grid size={{ xs: 12, sm: 8 }}>
                    <TextField fullWidth label="ASCOM ProgID" value={settings.ascomProgId}
                      onChange={(e) => update('ascomProgId', e.target.value)}
                      placeholder="ASCOM.Simulator.Camera"
                      helperText="Windows only. Example: ASCOM.Simulator.Camera" />
                  </Grid>
                )}
              </Grid>
            </CardContent>
          </Card>
        </Grid>

        {/* Exposure Settings (camera sources only) */}
        {(settings.sourceType === 'AlpacaCamera' || settings.sourceType === 'AscomCamera') && (
          <Grid size={12}>
            <Card>
              <CardContent>
                <Typography variant="h6" gutterBottom>Exposure Settings</Typography>
                <Grid container spacing={2}>
                  <Grid size={{ xs: 12, sm: 4 }}>
                    <TextField fullWidth label="Exposure Duration (s)" type="number"
                      value={settings.exposureDuration}
                      onChange={(e) => update('exposureDuration', Number(e.target.value))}
                      slotProps={{ htmlInput: { min: 0.001, step: 0.1 } }} />
                  </Grid>
                  <Grid size={{ xs: 12, sm: 4 }}>
                    <FormControlLabel
                      control={<Switch checked={settings.autoExposure}
                        onChange={(e) => update('autoExposure', e.target.checked)} />}
                      label="Auto-Exposure" />
                  </Grid>
                  {settings.autoExposure && (
                    <>
                      <Grid size={{ xs: 12, sm: 4 }}>
                        <TextField fullWidth label="Target Brightness (0-255)" type="number"
                          value={settings.autoExposureTarget}
                          onChange={(e) => update('autoExposureTarget', Number(e.target.value))}
                          slotProps={{ htmlInput: { min: 0, max: 255 } }} />
                      </Grid>
                      <Grid size={{ xs: 12, sm: 4 }}>
                        <TextField fullWidth label="Min Exposure (s)" type="number"
                          value={settings.exposureMin}
                          onChange={(e) => update('exposureMin', Number(e.target.value))}
                          slotProps={{ htmlInput: { min: 0.001, step: 0.1 } }} />
                      </Grid>
                      <Grid size={{ xs: 12, sm: 4 }}>
                        <TextField fullWidth label="Max Exposure (s)" type="number"
                          value={settings.exposureMax}
                          onChange={(e) => update('exposureMax', Number(e.target.value))}
                          slotProps={{ htmlInput: { min: 0.1, step: 1 } }} />
                      </Grid>
                    </>
                  )}
                </Grid>
              </CardContent>
            </Card>
          </Grid>
        )}

        {/* Common fields */}
        <Grid size={12}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom>Common Settings</Typography>
              <Grid container spacing={2}>
                <Grid size={{ xs: 12, sm: 4 }}>
                  <TextField
                    fullWidth
                    label="Frame Interval (s)"
                    type="number"
                    value={settings.frameIntervalSeconds}
                    onChange={(e) => update('frameIntervalSeconds', Number(e.target.value))}
                    slotProps={{ htmlInput: { min: 0.1, step: 0.1 } }}
                  />
                </Grid>
                <Grid size={{ xs: 12, sm: 4 }}>
                  <TextField
                    fullWidth
                    label="Target Width"
                    type="number"
                    value={settings.targetWidth}
                    onChange={(e) => update('targetWidth', Number(e.target.value))}
                    slotProps={{ htmlInput: { min: 1 } }}
                  />
                </Grid>
                <Grid size={{ xs: 12, sm: 4 }}>
                  <TextField
                    fullWidth
                    label="Target Height"
                    type="number"
                    value={settings.targetHeight}
                    onChange={(e) => update('targetHeight', Number(e.target.value))}
                    slotProps={{ htmlInput: { min: 1 } }}
                  />
                </Grid>
                <Grid size={{ xs: 12, sm: 4 }}>
                  <TextField
                    fullWidth
                    label="Reconnect Interval (s)"
                    type="number"
                    value={settings.reconnectIntervalSeconds}
                    onChange={(e) => update('reconnectIntervalSeconds', Number(e.target.value))}
                    slotProps={{ htmlInput: { min: 1 } }}
                  />
                </Grid>
                <Grid size={{ xs: 12, sm: 4 }}>
                  <FormControlLabel
                    control={
                      <Switch
                        checked={settings.flipHorizontal}
                        onChange={(e) => update('flipHorizontal', e.target.checked)}
                      />
                    }
                    label="Flip Horizontal"
                  />
                </Grid>
                <Grid size={{ xs: 12, sm: 4 }}>
                  <FormControlLabel
                    control={
                      <Switch
                        checked={settings.flipVertical}
                        onChange={(e) => update('flipVertical', e.target.checked)}
                      />
                    }
                    label="Flip Vertical"
                  />
                </Grid>
                <Grid size={{ xs: 12, sm: 4 }}>
                  <FormControl fullWidth>
                    <InputLabel id="rotation-label">Rotation Degrees</InputLabel>
                    <Select
                      labelId="rotation-label"
                      value={String(settings.rotationDegrees)}
                      label="Rotation Degrees"
                      onChange={handleRotationChange}
                    >
                      <MenuItem value="0">0</MenuItem>
                      <MenuItem value="90">90</MenuItem>
                      <MenuItem value="180">180</MenuItem>
                      <MenuItem value="270">270</MenuItem>
                    </Select>
                  </FormControl>
                </Grid>
              </Grid>
            </CardContent>
          </Card>
        </Grid>

        {/* Connect button */}
        <Grid size={12}>
          <Button
            variant="contained"
            size="large"
            onClick={handleSave}
            disabled={saving}
          >
            {saving ? 'Connecting...' : 'Connect'}
          </Button>
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
