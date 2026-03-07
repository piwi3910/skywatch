export interface CaptureSettings {
  sourceType: 'UsbCamera' | 'Rtsp' | 'Replay' | 'AlpacaCamera' | 'AscomCamera';
  sourceIdentifier: string;
  deviceIndex: number;
  frameIntervalSeconds: number;
  targetWidth: number;
  targetHeight: number;
  reconnectIntervalSeconds: number;
  staleFrameTimeoutSeconds: number;
  connectionTimeoutSeconds: number;
  flipHorizontal: boolean;
  flipVertical: boolean;
  rotationDegrees: number;
  // Alpaca
  alpacaHost: string;
  alpacaPort: number;
  alpacaDeviceNumber: number;
  // ASCOM COM
  ascomProgId: string;
  // Exposure
  exposureDuration: number;
  autoExposure: boolean;
  autoExposureTarget: number;
  exposureMin: number;
  exposureMax: number;
}

export interface ThresholdSettings {
  maxAverageBrightness: number;
  minStarCount: number;
  maxCloudinessScore: number;
  minEntropy: number;
  minEdgeDensity: number;
  obstructionThreshold: number;
  starDetectionThreshold: number;
  confidenceThreshold: number;
}

export interface TimingSettings {
  warmupSeconds: number;
  unsafeDebounceSeconds: number;
  safeRecoveryDebounceSeconds: number;
  minimumUnsafeHoldSeconds: number;
  failSafeOnCameraLoss: boolean;
  failSafeOnStaleFrame: boolean;
}

export interface ProfileSettings {
  name: string;
  description: string;
  capture: CaptureSettings;
  thresholds: ThresholdSettings;
  timing: TimingSettings;
  masks: unknown[];
  manualOverride: string;
}

export interface SkyMetrics {
  brightness: number;
  starCount: number;
  cloudiness: number;
  entropy: number;
  edgeDensity: number;
  obstructionScore: number;
}

export interface CaptureStatusInfo {
  isConnected: boolean;
  sourceType: string;
  sourceDescription: string;
  framesCaptured: number;
  errorMessage: string | null;
  currentExposureDuration: number | null;
  autoExposureEnabled: boolean;
  autoExposureTarget: number | null;
}

export interface StatusData {
  safetyState: string;
  isSafe: boolean;
  reasons: string;
  confidence: number;
  explanation: string;
  metrics: SkyMetrics;
  capture: CaptureStatusInfo;
}
