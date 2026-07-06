import { describe, expect, it } from 'vitest';

import { AUTH_SESSION_STORAGE_KEY, readAuthSession } from './authStorage';
import { createAuthSession } from '../../test/authTestUtils';

describe('authStorage', () => {
  it('clears and rejects stored sessions that do not include a userId', () => {
    const legacySession = createAuthSession({ userId: 'session-user' });
    const { userId: _ignored, ...legacyShape } = legacySession;

    localStorage.setItem(AUTH_SESSION_STORAGE_KEY, JSON.stringify(legacyShape));

    expect(readAuthSession()).toBeNull();
    expect(localStorage.getItem(AUTH_SESSION_STORAGE_KEY)).toBeNull();
  });
});
