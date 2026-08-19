import { describe, expect, it } from 'vitest';
import { parseExternalAuthCallback } from './ExternalAuthCallbackPage';

describe('external auth callback', () => {
  it('reads the token and preserves a local return route', () => {
    expect(parseExternalAuthCallback('#code=one-time-code&return_url=%2Frepositories%2F123')).toEqual({ code: 'one-time-code', error: null, returnUrl: '/repositories/123' });
  });

  it.each(['//attacker.example', '/\\attacker.example', 'https://attacker.example'])('rejects unsafe return route %s', returnUrl => {
    expect(parseExternalAuthCallback(`#error=denied&return_url=${encodeURIComponent(returnUrl)}`).returnUrl).toBe('/');
  });
});
