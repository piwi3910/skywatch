import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { ThemeProvider, CssBaseline } from '@mui/material';
import darkTheme from './theme/darkTheme';
import { SkyWatchProvider } from './context/SkyWatchContext';
import Layout from './components/Layout';
import Dashboard from './pages/Dashboard';
import Sources from './pages/Sources';
import Thresholds from './pages/Thresholds';
import Timing from './pages/Timing';
import Profiles from './pages/Profiles';
import System from './pages/System';

export default function App() {
  return (
    <ThemeProvider theme={darkTheme}>
      <CssBaseline />
      <SkyWatchProvider>
        <BrowserRouter>
          <Routes>
            <Route element={<Layout />}>
              <Route path="/" element={<Dashboard />} />
              <Route path="/sources" element={<Sources />} />
              <Route path="/thresholds" element={<Thresholds />} />
              <Route path="/timing" element={<Timing />} />
              <Route path="/profiles" element={<Profiles />} />
              <Route path="/system" element={<System />} />
            </Route>
          </Routes>
        </BrowserRouter>
      </SkyWatchProvider>
    </ThemeProvider>
  );
}
