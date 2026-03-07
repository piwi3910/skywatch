import { createTheme } from '@mui/material/styles';

const darkTheme = createTheme({
  palette: {
    mode: 'dark',
    primary: { main: '#58a6ff' },
    secondary: { main: '#3fb950' },
    error: { main: '#f85149' },
    warning: { main: '#d29922' },
    background: { default: '#0d1117', paper: '#161b22' },
    text: { primary: '#e6edf3', secondary: '#8b949e' },
  },
  typography: {
    fontFamily: '-apple-system, BlinkMacSystemFont, "Segoe UI", Helvetica, Arial, sans-serif',
  },
  components: {
    MuiCard: {
      styleOverrides: {
        root: { backgroundImage: 'none', border: '1px solid #30363d' },
      },
    },
  },
});

export default darkTheme;
