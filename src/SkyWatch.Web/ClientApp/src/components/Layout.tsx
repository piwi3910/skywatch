import { useState } from 'react';
import { Outlet, useNavigate, useLocation } from 'react-router-dom';
import {
  Box, Drawer, List, ListItemButton, ListItemIcon, ListItemText,
  AppBar, Toolbar, Typography, IconButton, useMediaQuery, useTheme,
} from '@mui/material';
import {
  Dashboard as DashboardIcon, Videocam as VideocamIcon,
  Tune as TuneIcon, Timer as TimerIcon,
  ManageAccounts as ProfilesIcon, Settings as SystemIcon,
  Menu as MenuIcon,
} from '@mui/icons-material';

const DRAWER_WIDTH = 240;

const navItems = [
  { label: 'Dashboard', path: '/', icon: <DashboardIcon /> },
  { label: 'Sources', path: '/sources', icon: <VideocamIcon /> },
  { label: 'Thresholds', path: '/thresholds', icon: <TuneIcon /> },
  { label: 'Timing', path: '/timing', icon: <TimerIcon /> },
  { label: 'Profiles', path: '/profiles', icon: <ProfilesIcon /> },
  { label: 'System', path: '/system', icon: <SystemIcon /> },
];

export default function Layout() {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('md'));
  const [mobileOpen, setMobileOpen] = useState(false);
  const navigate = useNavigate();
  const location = useLocation();

  const drawer = (
    <Box sx={{ overflow: 'auto' }}>
      <Toolbar>
        <Typography variant="h6" noWrap>SkyWatch</Typography>
      </Toolbar>
      <List>
        {navItems.map((item) => (
          <ListItemButton
            key={item.path}
            selected={location.pathname === item.path}
            onClick={() => { navigate(item.path); if (isMobile) setMobileOpen(false); }}
          >
            <ListItemIcon>{item.icon}</ListItemIcon>
            <ListItemText primary={item.label} />
          </ListItemButton>
        ))}
      </List>
    </Box>
  );

  return (
    <Box sx={{ display: 'flex' }}>
      {isMobile && (
        <AppBar position="fixed" sx={{ zIndex: theme.zIndex.drawer + 1 }}>
          <Toolbar>
            <IconButton color="inherit" edge="start" onClick={() => setMobileOpen(!mobileOpen)}>
              <MenuIcon />
            </IconButton>
            <Typography variant="h6" noWrap>SkyWatch</Typography>
          </Toolbar>
        </AppBar>
      )}
      <Drawer
        variant={isMobile ? 'temporary' : 'permanent'}
        open={isMobile ? mobileOpen : true}
        onClose={() => setMobileOpen(false)}
        sx={{ width: DRAWER_WIDTH, '& .MuiDrawer-paper': { width: DRAWER_WIDTH } }}
      >
        {drawer}
      </Drawer>
      <Box component="main" sx={{ flexGrow: 1, p: 3, mt: isMobile ? 8 : 0 }}>
        <Outlet />
      </Box>
    </Box>
  );
}
