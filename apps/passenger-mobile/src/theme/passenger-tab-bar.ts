export function createPassengerTabBarStyle(bottomInset: number) {
  return {
    position: 'absolute' as const,
    left: 16,
    right: 16,
    bottom: 12,
    height: 72 + bottomInset,
    borderTopWidth: 0,
    borderRadius: 32,
    borderCurve: 'continuous' as const,
    backgroundColor: '#FFFFFF',
    paddingHorizontal: 10,
    paddingTop: 2,
    paddingBottom: bottomInset,
    boxShadow: '0 8px 24px rgba(11,31,58,0.08)',
  };
}
