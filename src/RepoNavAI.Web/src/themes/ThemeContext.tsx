import { createContext, useContext, useEffect, useMemo, useState, type PropsWithChildren } from 'react';

export type ThemePreference = 'light' | 'dark' | 'system';
export type ResolvedTheme = 'light' | 'dark';
const THEME_KEY = 'reponav.theme';

interface ThemeContextValue { preference: ThemePreference; resolvedTheme: ResolvedTheme; setPreference: (preference: ThemePreference) => void }
const ThemeContext = createContext<ThemeContextValue | null>(null);

export function isThemePreference(value: string | null): value is ThemePreference { return value === 'light' || value === 'dark' || value === 'system'; }
export function resolveTheme(preference: ThemePreference, systemDark: boolean): ResolvedTheme { return preference === 'system' ? (systemDark ? 'dark' : 'light') : preference; }

export function ThemeProvider({ children }: PropsWithChildren) {
  const [preference, setPreferenceState] = useState<ThemePreference>(() => { const stored = localStorage.getItem(THEME_KEY); return isThemePreference(stored) ? stored : 'system'; });
  const [systemDark, setSystemDark] = useState(() => window.matchMedia('(prefers-color-scheme: dark)').matches);
  const resolvedTheme = resolveTheme(preference, systemDark);
  useEffect(() => {
    const query = window.matchMedia('(prefers-color-scheme: dark)');
    const update = (event: MediaQueryListEvent) => setSystemDark(event.matches);
    query.addEventListener('change', update);
    return () => query.removeEventListener('change', update);
  }, []);
  useEffect(() => { document.documentElement.dataset.theme = resolvedTheme; document.documentElement.style.colorScheme = resolvedTheme; }, [resolvedTheme]);
  function setPreference(next: ThemePreference) { localStorage.setItem(THEME_KEY, next); setPreferenceState(next); }
  const value = useMemo(() => ({ preference, resolvedTheme, setPreference }), [preference, resolvedTheme]);
  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}

export function useTheme() { const value = useContext(ThemeContext); if (!value) throw new Error('useTheme must be used inside ThemeProvider'); return value; }

export function ThemeControl() {
  const { preference, setPreference } = useTheme();
  return <label className="field">Theme<select value={preference} onChange={event => setPreference(event.target.value as ThemePreference)}><option value="system">System</option><option value="light">Light</option><option value="dark">Dark</option></select><span className="text-xs font-normal text-slate-500">System follows your operating-system appearance while RepoNavAI is open.</span></label>;
}
