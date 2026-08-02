type IconName =
  | 'overview' | 'drivers' | 'map' | 'users' | 'payments' | 'disputes'
  | 'audit' | 'search' | 'refresh' | 'logout' | 'check' | 'close'
  | 'file' | 'eye' | 'car' | 'route' | 'alert' | 'menu'

const paths: Record<IconName, string> = {
  overview: 'M4 13h6V4H4v9Zm10 7h6v-9h-6v9ZM4 20h6v-3H4v3Zm10-13h6V4h-6v3Z',
  drivers: 'M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2 M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8 M22 21v-2a4 4 0 0 0-3-3.87 M16 3.13a4 4 0 0 1 0 7.75',
  map: 'm3 6 6-3 6 3 6-3v15l-6 3-6-3-6 3V6Zm6-3v15m6-12v15',
  users: 'M16 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2 M8.5 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8 M20 8v6m3-3h-6',
  payments: 'M3 6h18v12H3z M3 10h18 M7 15h3',
  disputes: 'M21 15a4 4 0 0 1-4 4H8l-5 3V7a4 4 0 0 1 4-4h10a4 4 0 0 1 4 4v8Z M8 8h8m-8 4h5',
  audit: 'M9 11l3 3L22 4 M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11',
  search: 'm21 21-4.35-4.35 M19 11a8 8 0 1 1-16 0 8 8 0 0 1 16 0Z',
  refresh: 'M20 11a8 8 0 1 0-2.34 5.66 M20 4v7h-7',
  logout: 'M10 17l5-5-5-5m5 5H3m12-9h4a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-4',
  check: 'm20 6-11 11-5-5',
  close: 'M18 6 6 18M6 6l12 12',
  file: 'M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8Zm0 0v6h6M8 13h8m-8 4h6',
  eye: 'M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12Zm10 3a3 3 0 1 0 0-6 3 3 0 0 0 0 6Z',
  car: 'm5 17-1 2v2h2l1-2h10l1 2h2v-2l-1-2-2-7H7l-2 7Zm2-2h.01M17 15h.01M6 17h12M7 10l1-4h8l1 4',
  route: 'M6 19a3 3 0 1 0 0-6 3 3 0 0 0 0 6Zm12-8a3 3 0 1 0 0-6 3 3 0 0 0 0 6ZM8.5 15h3a3 3 0 0 0 3-3V8',
  alert: 'M12 9v4m0 4h.01M10.3 3.7 2.4 18a2 2 0 0 0 1.75 3h15.7a2 2 0 0 0 1.75-3L13.7 3.7a2 2 0 0 0-3.4 0Z',
  menu: 'M4 6h16M4 12h16M4 18h16',
}

export function Icon({ name, size = 20 }: { name: IconName; size?: number }) {
  const filled = name === 'overview'
  return (
    <svg aria-hidden="true" width={size} height={size} viewBox="0 0 24 24" fill={filled ? 'currentColor' : 'none'} stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
      <path d={paths[name]} />
    </svg>
  )
}
