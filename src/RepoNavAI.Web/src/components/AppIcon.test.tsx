import { renderToStaticMarkup } from 'react-dom/server';
import { Search } from 'lucide-react';
import { describe, expect, it } from 'vitest';
import { AppIcon } from './AppIcon';

describe('AppIcon', () => {
  it('hides decorative icons from assistive technology', () => {
    const markup = renderToStaticMarkup(<AppIcon icon={Search}/>);
    expect(markup).toContain('aria-hidden="true"');
    expect(markup).toContain('width="18"');
  });

  it('exposes a meaningful icon with its accessible name', () => {
    const markup = renderToStaticMarkup(<AppIcon icon={Search} size="lg" label="Search repository"/>);
    expect(markup).toContain('role="img"');
    expect(markup).toContain('aria-label="Search repository"');
    expect(markup).toContain('width="20"');
    expect(markup).not.toContain('aria-hidden');
  });
});
