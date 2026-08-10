import { describe, expect, it } from 'vitest';
import { isThemePreference, resolveTheme } from './ThemeContext';

describe('theme selection', () => {
  it('resolves explicit themes independently of the system', () => {
    expect(resolveTheme('light', true)).toBe('light');
    expect(resolveTheme('dark', false)).toBe('dark');
  });
  it('resolves system preference as it changes', () => {
    expect(resolveTheme('system', false)).toBe('light');
    expect(resolveTheme('system', true)).toBe('dark');
  });
  it('accepts only supported persisted values', () => {
    expect(isThemePreference('system')).toBe(true);
    expect(isThemePreference('sepia')).toBe(false);
    expect(isThemePreference(null)).toBe(false);
  });
});
