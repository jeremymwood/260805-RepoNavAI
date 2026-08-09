import { describe, expect, it } from 'vitest';
import { getNavigationItems } from './DashboardPage';

describe('workspace navigation', () => {
  it('shows administrative settings to owners and administrators', () => {
    expect(getNavigationItems('Owner').map(item => item.label)).toContain('Organization settings');
    expect(getNavigationItems('Administrator').map(item => item.label)).toContain('Organization settings');
  });

  it('does not advertise administrative settings to members', () => {
    const labels = getNavigationItems('Member').map(item => item.label);
    expect(labels).not.toContain('Organization settings');
    expect(labels).toContain('Organization members');
    expect(labels).toContain('Profile settings');
  });

  it('keeps repositories inside the overview instead of duplicating navigation', () => {
    expect(getNavigationItems('Owner').map(item => item.label)).not.toContain('Repositories');
  });
});
