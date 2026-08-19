import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { ExternalProviderIcon } from './ExternalLoginButtons';

describe('external provider icons', () => {
  it.each(['Google', 'Apple', 'Microsoft'])('renders a decorative %s mark', provider => {
    const markup = renderToStaticMarkup(<ExternalProviderIcon provider={provider}/>);
    expect(markup).toContain('<svg');
    expect(markup).toContain('aria-hidden="true"');
  });
});
