import React from 'react';
import ReactDOM from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter } from 'react-router-dom';
import { App } from './App';
import { AuthProvider } from './auth/AuthContext';
import './styles.css';
import { OrganizationProvider } from './organizations/OrganizationContext';
import { DemoPage } from './demo/DemoPage';
import { ThemeProvider } from './themes/ThemeContext';
const queryClient = new QueryClient({ defaultOptions: { queries: { retry: 1, staleTime: 30_000 } } });
const content = import.meta.env.VITE_PUBLIC_DEMO === 'true'
  ? <DemoPage/>
  : <QueryClientProvider client={queryClient}><BrowserRouter><AuthProvider><OrganizationProvider><App /></OrganizationProvider></AuthProvider></BrowserRouter></QueryClientProvider>;
ReactDOM.createRoot(document.getElementById('root')!).render(<React.StrictMode><ThemeProvider>{content}</ThemeProvider></React.StrictMode>);
