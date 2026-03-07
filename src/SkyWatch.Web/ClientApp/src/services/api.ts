import type { CaptureSettings, ThresholdSettings, TimingSettings, ProfileSettings } from './types';

const BASE = '/api';

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    headers: { 'Content-Type': 'application/json' },
    ...options,
  });
  if (!res.ok) {
    const body = await res.json().catch(() => ({}));
    throw new Error(body.error || body.errors?.join(', ') || `Request failed: ${res.status}`);
  }
  return res.json();
}

export const api = {
  // Dashboard
  getStatus: () => request<unknown>('/dashboard/status'),

  // Capture
  getCaptureSettings: () => request<CaptureSettings>('/config/capture'),
  updateCaptureSettings: (settings: CaptureSettings) =>
    request<CaptureSettings>('/config/capture', { method: 'POST', body: JSON.stringify(settings) }),

  // Thresholds
  getThresholds: () => request<ThresholdSettings>('/config/thresholds'),
  updateThresholds: (settings: ThresholdSettings) =>
    request<ThresholdSettings>('/config/thresholds', { method: 'PUT', body: JSON.stringify(settings) }),

  // Timing
  getTiming: () => request<TimingSettings>('/config/timing'),
  updateTiming: (settings: TimingSettings) =>
    request<TimingSettings>('/config/timing', { method: 'PUT', body: JSON.stringify(settings) }),

  // Override
  getOverride: () => request<{ mode: string }>('/config/override'),
  updateOverride: (mode: string) =>
    request<{ mode: string }>('/config/override', { method: 'PUT', body: JSON.stringify({ mode }) }),

  // Profiles
  getProfile: () => request<ProfileSettings>('/config/profile'),
  updateProfile: (profile: ProfileSettings) =>
    request<ProfileSettings>('/config/profile', { method: 'PUT', body: JSON.stringify(profile) }),
  listProfiles: () => request<string[]>('/config/profiles'),
  activateProfile: (name: string) =>
    request<ProfileSettings>(`/config/profiles/${encodeURIComponent(name)}/activate`, { method: 'POST' }),

  // Preview fallback
  getPreviewFrameUrl: () => `/api/preview/frame?t=${Date.now()}`,
};
