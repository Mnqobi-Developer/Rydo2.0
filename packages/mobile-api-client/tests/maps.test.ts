import { describe, expect, it } from 'vitest';

import { decodeGooglePolyline } from '../src/maps';

describe('decodeGooglePolyline', () => {
  it('decodes the Google reference polyline', () => {
    expect(decodeGooglePolyline('_p~iF~ps|U_ulLnnqC_mqNvxq`@')).toEqual([
      { latitude: 38.5, longitude: -120.2 },
      { latitude: 40.7, longitude: -120.95 },
      { latitude: 43.252, longitude: -126.453 },
    ]);
  });

  it('rejects truncated polylines', () => {
    expect(() => decodeGooglePolyline('_')).toThrow('Invalid encoded polyline.');
  });
});
