import { useState, useEffect, useCallback, useRef } from 'react';
import {
  Box, Typography, Card, CardContent, Button, List, ListItem,
  ListItemText, ListItemSecondaryAction, Chip, Dialog, DialogTitle,
  DialogContent, DialogActions, TextField, Snackbar, Alert,
  CircularProgress, Stack, Divider,
} from '@mui/material';
import { api } from '../services/api';
import type { ProfileSettings } from '../services/types';

export default function Profiles() {
  const [profiles, setProfiles] = useState<string[]>([]);
  const [activeProfile, setActiveProfile] = useState<string>('');
  const [loading, setLoading] = useState(true);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [newName, setNewName] = useState('');
  const [newDescription, setNewDescription] = useState('');
  const [creating, setCreating] = useState(false);
  const [snackbar, setSnackbar] = useState<{ open: boolean; message: string; severity: 'success' | 'error' }>({
    open: false, message: '', severity: 'success',
  });
  const fileInputRef = useRef<HTMLInputElement>(null);

  const showMessage = (message: string, severity: 'success' | 'error') => {
    setSnackbar({ open: true, message, severity });
  };

  const loadData = useCallback(async () => {
    try {
      setLoading(true);
      const [profileList, current] = await Promise.all([
        api.listProfiles(),
        api.getProfile(),
      ]);
      setProfiles(profileList);
      setActiveProfile(current.name);
    } catch (err) {
      showMessage(`Failed to load profiles: ${err instanceof Error ? err.message : String(err)}`, 'error');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadData();
  }, [loadData]);

  const handleActivate = async (name: string) => {
    try {
      await api.activateProfile(name);
      setActiveProfile(name);
      showMessage(`Profile "${name}" activated.`, 'success');
      await loadData();
    } catch (err) {
      showMessage(`Failed to activate profile: ${err instanceof Error ? err.message : String(err)}`, 'error');
    }
  };

  const handleCreate = async () => {
    if (!newName.trim()) {
      showMessage('Profile name is required.', 'error');
      return;
    }
    try {
      setCreating(true);
      const current = await api.getProfile();
      const newProfile: ProfileSettings = {
        ...current,
        name: newName.trim(),
        description: newDescription.trim(),
      };
      await api.updateProfile(newProfile);
      setDialogOpen(false);
      setNewName('');
      setNewDescription('');
      showMessage(`Profile "${newProfile.name}" created.`, 'success');
      await loadData();
    } catch (err) {
      showMessage(`Failed to create profile: ${err instanceof Error ? err.message : String(err)}`, 'error');
    } finally {
      setCreating(false);
    }
  };

  const handleExport = async () => {
    try {
      const profile = await api.getProfile();
      const json = JSON.stringify(profile, null, 2);
      const blob = new Blob([json], { type: 'application/json' });
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `${profile.name || 'profile'}.json`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      URL.revokeObjectURL(url);
      showMessage('Profile exported successfully.', 'success');
    } catch (err) {
      showMessage(`Failed to export profile: ${err instanceof Error ? err.message : String(err)}`, 'error');
    }
  };

  const handleImport = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) return;
    try {
      const text = await file.text();
      const parsed = JSON.parse(text) as ProfileSettings;
      if (!parsed.name) {
        showMessage('Invalid profile: missing name.', 'error');
        return;
      }
      await api.updateProfile(parsed);
      showMessage(`Profile "${parsed.name}" imported successfully.`, 'success');
      await loadData();
    } catch (err) {
      showMessage(`Failed to import profile: ${err instanceof Error ? err.message : String(err)}`, 'error');
    } finally {
      if (fileInputRef.current) {
        fileInputRef.current.value = '';
      }
    }
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
      <Typography variant="h4" gutterBottom>Profiles</Typography>

      <Card sx={{ mb: 3 }}>
        <CardContent>
          <Stack direction="row" spacing={2} sx={{ mb: 2 }}>
            <Button variant="contained" onClick={() => setDialogOpen(true)}>
              Create Profile
            </Button>
            <Button variant="outlined" onClick={handleExport}>
              Export Current
            </Button>
            <Button variant="outlined" onClick={() => fileInputRef.current?.click()}>
              Import
            </Button>
            <input
              ref={fileInputRef}
              type="file"
              accept=".json"
              hidden
              onChange={handleImport}
            />
          </Stack>

          <Divider sx={{ mb: 2 }} />

          {profiles.length === 0 ? (
            <Typography color="text.secondary">No profiles found.</Typography>
          ) : (
            <List>
              {profiles.map((name) => (
                <ListItem key={name} divider>
                  <ListItemText
                    primary={
                      <Stack direction="row" spacing={1} alignItems="center">
                        <Typography>{name}</Typography>
                        {name === activeProfile && (
                          <Chip label="Active" color="primary" size="small" />
                        )}
                      </Stack>
                    }
                  />
                  <ListItemSecondaryAction>
                    <Button
                      size="small"
                      variant="outlined"
                      disabled={name === activeProfile}
                      onClick={() => void handleActivate(name)}
                    >
                      Activate
                    </Button>
                  </ListItemSecondaryAction>
                </ListItem>
              ))}
            </List>
          )}
        </CardContent>
      </Card>

      <Dialog open={dialogOpen} onClose={() => setDialogOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Create New Profile</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField
              label="Profile Name"
              fullWidth
              value={newName}
              onChange={(e) => setNewName(e.target.value)}
              autoFocus
            />
            <TextField
              label="Description"
              fullWidth
              multiline
              rows={2}
              value={newDescription}
              onChange={(e) => setNewDescription(e.target.value)}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)}>Cancel</Button>
          <Button
            variant="contained"
            onClick={() => void handleCreate()}
            disabled={creating || !newName.trim()}
          >
            {creating ? 'Creating...' : 'Create'}
          </Button>
        </DialogActions>
      </Dialog>

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
