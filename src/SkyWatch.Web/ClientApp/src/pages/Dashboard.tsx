import {
  Grid, Card, CardContent, Typography, Box, Chip, Stack,
} from '@mui/material';
import {
  Shield as ShieldIcon,
  Info as InfoIcon,
} from '@mui/icons-material';
import { useSkyWatch } from '../context/SkyWatchContext';
import StatusBanner from '../components/StatusBanner';
import LivePreview from '../components/LivePreview';
import MetricsPanel from '../components/MetricsPanel';

function formatReasons(reasons: string): string[] {
  if (!reasons || reasons === 'None') return [];
  return reasons.split(',').map((r) => r.trim()).filter(Boolean);
}

export default function Dashboard() {
  const { status, previewUrl, connected } = useSkyWatch();
  const reasons = status ? formatReasons(status.reasons) : [];

  return (
    <Box>
      <Typography variant="h4" gutterBottom>Dashboard</Typography>

      <StatusBanner status={status} connected={connected} />

      <Grid container spacing={3}>
        {/* Live Preview - left / wider */}
        <Grid size={{ xs: 12, md: 7, lg: 8 }}>
          <LivePreview previewUrl={previewUrl} capture={status?.capture} />
        </Grid>

        {/* Metrics Panel - right */}
        <Grid size={{ xs: 12, md: 5, lg: 4 }}>
          <MetricsPanel metrics={status?.metrics} confidence={status?.confidence} />
        </Grid>

        {/* Safety Details - full width */}
        <Grid size={12}>
          <Card>
            <CardContent>
              <Stack direction="row" alignItems="center" spacing={1} sx={{ mb: 2 }}>
                <ShieldIcon color={status?.isSafe ? 'success' : 'error'} />
                <Typography variant="h6">Safety Details</Typography>
              </Stack>

              {!status ? (
                <Typography variant="body2" color="text.secondary">
                  No safety data available yet.
                </Typography>
              ) : (
                <Grid container spacing={2}>
                  <Grid size={{ xs: 12, sm: 6, md: 3 }}>
                    <Typography variant="caption" color="text.secondary">State</Typography>
                    <Typography variant="body1" fontWeight="bold">
                      {status.safetyState}
                    </Typography>
                  </Grid>

                  <Grid size={{ xs: 12, sm: 6, md: 3 }}>
                    <Typography variant="caption" color="text.secondary">Safe</Typography>
                    <Typography
                      variant="body1"
                      fontWeight="bold"
                      color={status.isSafe ? 'success.main' : 'error.main'}
                    >
                      {status.isSafe ? 'Yes' : 'No'}
                    </Typography>
                  </Grid>

                  <Grid size={{ xs: 12, sm: 6, md: 3 }}>
                    <Typography variant="caption" color="text.secondary">Confidence</Typography>
                    <Typography variant="body1" fontWeight="bold">
                      {(status.confidence * 100).toFixed(1)}%
                    </Typography>
                  </Grid>

                  <Grid size={{ xs: 12, sm: 6, md: 3 }}>
                    <Typography variant="caption" color="text.secondary">Reasons</Typography>
                    <Box sx={{ mt: 0.5 }}>
                      {reasons.length === 0 ? (
                        <Chip label="None" size="small" color="success" variant="outlined" />
                      ) : (
                        <Stack direction="row" spacing={0.5} flexWrap="wrap" useFlexGap>
                          {reasons.map((reason) => (
                            <Chip
                              key={reason}
                              label={reason}
                              size="small"
                              color="error"
                              variant="outlined"
                            />
                          ))}
                        </Stack>
                      )}
                    </Box>
                  </Grid>

                  <Grid size={12}>
                    <Stack direction="row" alignItems="flex-start" spacing={1}>
                      <InfoIcon fontSize="small" sx={{ mt: 0.3, color: 'text.secondary' }} />
                      <Box>
                        <Typography variant="caption" color="text.secondary">Explanation</Typography>
                        <Typography variant="body2">
                          {status.explanation || 'No explanation provided.'}
                        </Typography>
                      </Box>
                    </Stack>
                  </Grid>
                </Grid>
              )}
            </CardContent>
          </Card>
        </Grid>
      </Grid>
    </Box>
  );
}
