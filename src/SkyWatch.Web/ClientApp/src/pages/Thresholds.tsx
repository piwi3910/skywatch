import { useState, useEffect, useCallback } from 'react';
import {
  Box, Typography, Card, CardContent, Grid, TextField, Button,
  Slider, Snackbar, Alert, CircularProgress, Stack, Chip,
} from '@mui/material';
import { api } from '../services/api';
import { useSkyWatch } from '../context/SkyWatchContext';
import type { ThresholdSettings } from '../services/types';

const defaultSettings: ThresholdSettings = {
  maxAverageBrightness: 200,
  minStarCount: 10,
  maxCloudinessScore: 0.5,
  minEntropy: 3.0,
  minEdgeDensity: 0.01,
  obstructionThreshold: 0.5,
  starDetectionThreshold: 0.5,
  confidenceThreshold: 0.7,
};

interface ThresholdFieldConfig {
  key: keyof ThresholdSettings;
  label: string;
  min: number;
  max: number;
  step: number;
  metricKey?: string;
  metricLabel?: string;
}

const fields: ThresholdFieldConfig[] = [
  { key: 'maxAverageBrightness', label: 'Max Average Brightness', min: 0, max: 255, step: 1, metricKey: 'brightness', metricLabel: 'Brightness' },
  { key: 'minStarCount', label: 'Min Star Count', min: 0, max: 100, step: 1, metricKey: 'starCount', metricLabel: 'Star Count' },
  { key: 'maxCloudinessScore', label: 'Max Cloudiness Score', min: 0, max: 1, step: 0.01, metricKey: 'cloudiness', metricLabel: 'Cloudiness' },
  { key: 'minEntropy', label: 'Min Entropy', min: 0, max: 8, step: 0.1, metricKey: 'entropy', metricLabel: 'Entropy' },
  { key: 'minEdgeDensity', label: 'Min Edge Density', min: 0, max: 0.1, step: 0.001, metricKey: 'edgeDensity', metricLabel: 'Edge Density' },
  { key: 'obstructionThreshold', label: 'Obstruction Threshold', min: 0, max: 1, step: 0.01, metricKey: 'obstructionScore', metricLabel: 'Obstruction' },
  { key: 'starDetectionThreshold', label: 'Star Detection Threshold', min: 0, max: 1, step: 0.01 },
  { key: 'confidenceThreshold', label: 'Confidence Threshold', min: 0, max: 1, step: 0.01 },
];

function getLiveValue(metrics: Record<string, number> | undefined, metricKey: string | undefined): number | null {
  if (!metrics || !metricKey) return null;
  const value = metrics[metricKey as keyof typeof metrics];
  return typeof value === 'number' ? value : null;
}

function formatValue(value: number, step: number): string {
  if (step >= 1) return String(Math.round(value));
  const decimals = Math.max(0, -Math.floor(Math.log10(step)));
  return value.toFixed(decimals);
}

export default function Thresholds() {
  const { status } = useSkyWatch();
  const [settings, setSettings] = useState<ThresholdSettings>(defaultSettings);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [snackbar, setSnackbar] = useState<{ open: boolean; message: string; severity: 'success' | 'error' }>({
    open: false, message: '', severity: 'success',
  });

  const loadSettings = useCallback(async () => {
    try {
      setLoading(true);
      const data = await api.getThresholds();
      setSettings(data);
    } catch (err) {
      setSnackbar({ open: true, message: `Failed to load thresholds: ${err instanceof Error ? err.message : String(err)}`, severity: 'error' });
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
      const updated = await api.updateThresholds(settings);
      setSettings(updated);
      setSnackbar({ open: true, message: 'Thresholds saved successfully.', severity: 'success' });
    } catch (err) {
      setSnackbar({ open: true, message: `Failed to save thresholds: ${err instanceof Error ? err.message : String(err)}`, severity: 'error' });
    } finally {
      setSaving(false);
    }
  };

  const updateField = (key: keyof ThresholdSettings, value: number) => {
    setSettings((prev) => ({ ...prev, [key]: value }));
  };

  const metrics = status?.metrics as Record<string, number> | undefined;

  if (loading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}>
        <CircularProgress />
      </Box>
    );
  }

  return (
    <Box>
      <Typography variant="h4" gutterBottom>Thresholds</Typography>

      <Card>
        <CardContent>
          <Stack spacing={4}>
            {fields.map((field) => {
              const currentValue = settings[field.key];
              const liveValue = getLiveValue(metrics, field.metricKey);
              const liveInRange = liveValue !== null && liveValue >= field.min && liveValue <= field.max;

              return (
                <Box key={field.key}>
                  <Stack direction="row" alignItems="center" spacing={1} sx={{ mb: 1 }}>
                    <Typography variant="subtitle1" fontWeight="bold">
                      {field.label}
                    </Typography>
                    {liveValue !== null && (
                      <Chip
                        label={`Live: ${formatValue(liveValue, field.step)}`}
                        size="small"
                        color="info"
                        variant="outlined"
                      />
                    )}
                  </Stack>
                  <Grid container spacing={2} alignItems="center">
                    <Grid size={{ xs: 12, sm: 9 }}>
                      <Slider
                        value={currentValue}
                        min={field.min}
                        max={field.max}
                        step={field.step}
                        onChange={(_e, val) => updateField(field.key, val as number)}
                        valueLabelDisplay="auto"
                        valueLabelFormat={(v) => formatValue(v, field.step)}
                        marks={liveInRange ? [
                          { value: liveValue, label: '' },
                        ] : undefined}
                        sx={liveInRange ? {
                          '& .MuiSlider-mark': {
                            width: 8,
                            height: 8,
                            borderRadius: '50%',
                            backgroundColor: 'info.main',
                            transform: 'translate(-50%, -50%)',
                          },
                        } : undefined}
                      />
                    </Grid>
                    <Grid size={{ xs: 12, sm: 3 }}>
                      <TextField
                        fullWidth
                        type="number"
                        size="small"
                        value={currentValue}
                        onChange={(e) => {
                          const val = Number(e.target.value);
                          if (!Number.isNaN(val)) {
                            updateField(field.key, Math.min(field.max, Math.max(field.min, val)));
                          }
                        }}
                        slotProps={{ htmlInput: { min: field.min, max: field.max, step: field.step } }}
                      />
                    </Grid>
                  </Grid>
                </Box>
              );
            })}
          </Stack>

          <Box sx={{ mt: 4 }}>
            <Button
              variant="contained"
              size="large"
              onClick={handleSave}
              disabled={saving}
            >
              {saving ? 'Saving...' : 'Save'}
            </Button>
          </Box>
        </CardContent>
      </Card>

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
