import axios from 'axios';
import { describe, expect, it } from 'vitest';
import { classifyApiFailure } from './client';

describe('API failure classification', () => {
  it('treats a rejected token as unauthorized', () => {
    expect(classifyApiFailure(new axios.AxiosError('rejected', 'ERR_BAD_REQUEST', undefined, undefined, { status: 401 } as never))).toBe('unauthorized');
  });

  it.each([500, 502, 503])('treats HTTP %s as unavailable', status => {
    expect(classifyApiFailure(new axios.AxiosError('down', 'ERR_BAD_RESPONSE', undefined, undefined, { status } as never))).toBe('unavailable');
  });

  it('treats network failures and timeouts as unavailable', () => {
    expect(classifyApiFailure(new axios.AxiosError('Network Error', 'ERR_NETWORK'))).toBe('unavailable');
    expect(classifyApiFailure(new axios.AxiosError('timeout', 'ECONNABORTED'))).toBe('unavailable');
  });

  it('does not mistake ordinary validation failures for an outage', () => {
    expect(classifyApiFailure(new axios.AxiosError('invalid', 'ERR_BAD_REQUEST', undefined, undefined, { status: 400 } as never))).toBe('other');
  });
});
