import {
  Card, CardContent, Typography, Box, Alert, Chip, Stack,
} from '@mui/material';
import {
  Videocam as VideocamIcon,
  VideocamOff as VideocamOffIcon,
} from '@mui/icons-material';
import type { CaptureStatusInfo } from '../services/types';

interface LivePreviewProps {
  previewUrl: string | null;
  capture: CaptureStatusInfo | undefined;
}

export default function LivePreview({ previewUrl, capture }: LivePreviewProps) {
  return (
    <Card>
      <CardContent>
        <Typography variant="h6" gutterBottom>Live Preview</Typography>

        <Box
          sx={{
            width: '100%',
            aspectRatio: '16 / 9',
            backgroundColor: 'background.default',
            borderRadius: 1,
            overflow: 'hidden',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            mb: 2,
          }}
        >
          {previewUrl ? (
            <Box
              component="img"
              src={previewUrl}
              alt="Sky camera preview"
              sx={{ width: '100%', height: '100%', objectFit: 'contain' }}
            />
          ) : (
            <Typography variant="body2" color="text.secondary">
              No preview available
            </Typography>
          )}
        </Box>

        {capture && (
          <>
            <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap sx={{ mb: 1 }}>
              <Chip
                icon={capture.isConnected ? <VideocamIcon /> : <VideocamOffIcon />}
                label={capture.isConnected ? 'Connected' : 'Disconnected'}
                color={capture.isConnected ? 'success' : 'error'}
                size="small"
                variant="outlined"
              />
              <Chip
                label={capture.sourceType}
                size="small"
                variant="outlined"
              />
              <Chip
                label={`${capture.framesCaptured} frames`}
                size="small"
                variant="outlined"
              />
            </Stack>

            {capture.sourceDescription && (
              <Typography variant="caption" color="text.secondary" display="block" sx={{ mb: 1 }}>
                {capture.sourceDescription}
              </Typography>
            )}

            {capture.errorMessage && (
              <Alert severity="error" sx={{ mt: 1 }}>
                {capture.errorMessage}
              </Alert>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}
