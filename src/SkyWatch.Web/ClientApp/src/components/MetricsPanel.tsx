import {
  Card, CardContent, Typography, Box, LinearProgress, Stack,
} from '@mui/material';
import type { SkyMetrics } from '../services/types';

interface MetricsPanelProps {
  metrics: SkyMetrics | undefined;
  confidence: number | undefined;
}

interface MetricRowProps {
  label: string;
  value: string;
  progress: number; // 0-100
  color?: 'primary' | 'secondary' | 'error' | 'warning' | 'info';
}

function MetricRow({ label, value, progress, color = 'primary' }: MetricRowProps) {
  const clampedProgress = Math.min(100, Math.max(0, progress));
  return (
    <Box sx={{ mb: 1.5 }}>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 0.5 }}>
        <Typography variant="body2" color="text.secondary">{label}</Typography>
        <Typography variant="body2" fontWeight="bold">{value}</Typography>
      </Box>
      <LinearProgress
        variant="determinate"
        value={clampedProgress}
        color={color}
        sx={{ height: 6, borderRadius: 3 }}
      />
    </Box>
  );
}

export default function MetricsPanel({ metrics, confidence }: MetricsPanelProps) {
  if (!metrics) {
    return (
      <Card>
        <CardContent>
          <Typography variant="h6" gutterBottom>Current Metrics</Typography>
          <Typography variant="body2" color="text.secondary">
            No metrics available yet.
          </Typography>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardContent>
        <Typography variant="h6" gutterBottom>Current Metrics</Typography>
        <Stack spacing={0}>
          <MetricRow
            label="Brightness"
            value={metrics.brightness.toFixed(1)}
            progress={(metrics.brightness / 255) * 100}
          />
          <MetricRow
            label="Star Count"
            value={String(metrics.starCount)}
            progress={Math.min(metrics.starCount, 100)}
          />
          <MetricRow
            label="Cloudiness"
            value={`${(metrics.cloudiness * 100).toFixed(1)}%`}
            progress={metrics.cloudiness * 100}
            color={metrics.cloudiness > 0.7 ? 'error' : 'primary'}
          />
          <MetricRow
            label="Entropy"
            value={metrics.entropy.toFixed(2)}
            progress={(metrics.entropy / 8) * 100}
          />
          <MetricRow
            label="Edge Density"
            value={metrics.edgeDensity.toFixed(4)}
            progress={(metrics.edgeDensity / 0.5) * 100}
          />
          <MetricRow
            label="Obstruction"
            value={`${(metrics.obstructionScore * 100).toFixed(1)}%`}
            progress={metrics.obstructionScore * 100}
            color={metrics.obstructionScore > 0.5 ? 'error' : 'primary'}
          />
          <MetricRow
            label="Confidence"
            value={`${((confidence ?? 0) * 100).toFixed(1)}%`}
            progress={(confidence ?? 0) * 100}
            color="secondary"
          />
        </Stack>
      </CardContent>
    </Card>
  );
}
