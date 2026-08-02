import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import './App.css'
import { AdminApi, ApiError, readSession, writeSession } from './api'
import { appConfig } from './config/environment'
import { Icon } from './Icon'
import { LiveMap } from './LiveMap'
import type {
  AdminSession,
  DashboardData,
  Dispute,
  DriverApplication,
  DriverDocument,
  DriverStatus,
} from './types'

type View = 'overview' | 'drivers' | 'operations' | 'users' | 'payments' | 'disputes' | 'audit'
type IconName = Parameters<typeof Icon>[0]['name']

const navItems: { id: View; label: string; icon: IconName }[] = [
  { id: 'overview', label: 'Overview', icon: 'overview' },
  { id: 'drivers', label: 'Driver reviews', icon: 'drivers' },
  { id: 'operations', label: 'Live operations', icon: 'map' },
  { id: 'users', label: 'Users', icon: 'users' },
  { id: 'payments', label: 'Payments', icon: 'payments' },
  { id: 'disputes', label: 'Disputes', icon: 'disputes' },
  { id: 'audit', label: 'Audit trail', icon: 'audit' },
]

const documentLabels: Record<DriverDocument['documentType'], string> = {
  IdentityDocument: 'Identity document',
  DriversLicense: "Driver's licence",
  ProfessionalDrivingPermit: 'Professional driving permit',
}

const rejectionPresets = [
  'The image is blurred. Upload a clear image with all four corners visible.',
  'The document is cropped or incomplete. Upload every page in full.',
  'The document has expired. Upload a current, valid document.',
  'The details do not match your driver profile. Check and upload the correct document.',
]

function formatDate(value: string | null | undefined) {
  if (!value) return '—'
  return new Intl.DateTimeFormat('en-ZA', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
}

function money(value: number | null, currency = 'ZAR') {
  return value == null ? '—' : new Intl.NumberFormat('en-ZA', { style: 'currency', currency }).format(value)
}

function shortId(value: string) { return value.slice(0, 8).toUpperCase() }

function StatusBadge({ status }: { status: string }) {
  const tone = status === 'Approved' || status === 'Paid' || status === 'Completed' || status === 'Resolved'
    ? 'success'
    : status === 'Rejected' || status === 'Failed' || status === 'Cancelled'
      ? 'danger'
      : status === 'PendingReview' || status === 'UnderReview' || status === 'AwaitingPayment'
        ? 'warning'
        : 'info'
  return <span className={`status-badge status-${tone}`}>{status.replace(/([a-z])([A-Z])/g, '$1 $2')}</span>
}

function Login({ api, onLogin }: { api: AdminApi; onLogin(session: AdminSession): void }) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  async function submit(event: React.FormEvent) {
    event.preventDefault()
    setBusy(true); setError('')
    try { onLogin(await api.login(email, password)) }
    catch (reason) {
      if (reason instanceof ApiError && reason.status === 401) {
        setError('Email or password is incorrect.')
      } else if (reason instanceof ApiError && reason.status === 503) {
        setError('Admin access has not been configured on the API.')
      } else if (reason instanceof ApiError) {
        setError(`The RYDO API could not complete sign-in (${reason.status}).`)
      } else {
        setError('Unable to reach the RYDO API. Check that the API container is healthy.')
      }
    }
    finally { setBusy(false) }
  }
  return (
    <main className="login-page">
      <section className="login-card">
        <div className="brand-mark">R</div>
        <p className="eyebrow">RYDO operations</p>
        <h1>Welcome back</h1>
        <p className="muted">Sign in to review drivers and manage live operations.</p>
        <form onSubmit={submit}>
          <label>Email address<input type="email" autoComplete="username" required value={email} onChange={(event) => setEmail(event.target.value)} placeholder="admin@rydo.co.za" /></label>
          <label>Password<input type="password" autoComplete="current-password" minLength={16} required value={password} onChange={(event) => setPassword(event.target.value)} /></label>
          {error && <div className="inline-alert"><Icon name="alert" />{error}</div>}
          <button className="button button-primary button-wide" disabled={busy}>{busy ? 'Signing in…' : 'Sign in securely'}</button>
        </form>
        <span className="security-note">Protected admin access · Actions are audited</span>
      </section>
    </main>
  )
}

function DriverReview({ driver, api, onClose, onChanged }: { driver: DriverApplication; api: AdminApi; onClose(): void; onChanged(): void }) {
  const [selectedDocument, setSelectedDocument] = useState<DriverDocument | null>(null)
  const [previewUrl, setPreviewUrl] = useState('')
  const [reason, setReason] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const isPending = driver.profile.onboardingStatus === 'PendingReview'
  const canApprove = isPending && driver.documents.length === 3 && driver.documents.every((item) => item.reviewStatus !== 'Rejected') && !!driver.vehicle && driver.vehicle.reviewStatus !== 'Rejected'

  useEffect(() => () => { if (previewUrl) URL.revokeObjectURL(previewUrl) }, [previewUrl])
  async function openDocument(document: DriverDocument) {
    setSelectedDocument(document); setReason(''); setError(''); setPreviewUrl('')
    try { setPreviewUrl(await api.openDocument(driver.profile.userId, document.id)) }
    catch (requestError) { setError(requestError instanceof Error ? requestError.message : 'Document could not be opened.') }
  }
  async function decideDocument(approve: boolean) {
    if (!selectedDocument || (!approve && !reason.trim())) { setError('Explain what is invalid so the driver knows what to correct.'); return }
    setBusy(true); setError('')
    try { await api.reviewDocument(driver.profile.userId, selectedDocument.id, approve, reason); setSelectedDocument(null); onChanged() }
    catch (requestError) { setError(requestError instanceof Error ? requestError.message : 'Review failed.') }
    finally { setBusy(false) }
  }
  async function decideDriver(approve: boolean) {
    const rejectionReason = approve ? '' : window.prompt('Tell the driver why the application was rejected:') ?? ''
    if (!approve && !rejectionReason.trim()) return
    setBusy(true); setError('')
    try { await api.reviewDriver(driver.profile.userId, approve, rejectionReason); onChanged(); onClose() }
    catch (requestError) { setError(requestError instanceof Error ? requestError.message : 'Review failed.') }
    finally { setBusy(false) }
  }
  return (
    <div className="modal-backdrop" role="presentation">
      <section className="review-drawer" role="dialog" aria-modal="true" aria-label="Driver application review">
        <header className="drawer-header">
          <div><p className="eyebrow">Application {shortId(driver.profile.userId)}</p><h2>{driver.profile.firstName} {driver.profile.lastName}</h2><StatusBadge status={driver.profile.onboardingStatus} /></div>
          <button className="icon-button" onClick={onClose} aria-label="Close"><Icon name="close" /></button>
        </header>
        <div className="drawer-content">
          {driver.profile.rejectionReason && <div className="inline-alert"><Icon name="alert" />{driver.profile.rejectionReason}</div>}
          <section className="detail-section"><h3>Driver identity</h3><div className="detail-grid"><div><span>Email</span><strong>{driver.profile.email ?? 'Not provided'}</strong></div><div><span>Submitted</span><strong>{formatDate(driver.profile.submittedAt)}</strong></div></div></section>
          <section className="detail-section"><div className="section-title"><h3>Documents</h3><span>{driver.documents.length}/3 received</span></div><div className="document-list">
            {driver.documents.map((document) => <button className="document-row" key={document.id} onClick={() => void openDocument(document)}><span className="soft-icon"><Icon name="file" /></span><span><strong>{documentLabels[document.documentType]}</strong><small>{document.originalFileName} · {(document.sizeBytes / 1024 / 1024).toFixed(1)} MB</small></span><StatusBadge status={document.reviewStatus} /><Icon name="eye" /></button>)}
          </div></section>
          <section className="detail-section"><h3>Vehicle</h3>{driver.vehicle ? <div className="vehicle-card"><span className="soft-icon"><Icon name="car" /></span><div><strong>{driver.vehicle.year} {driver.vehicle.make} {driver.vehicle.model}</strong><span>{driver.vehicle.color} · {driver.vehicle.registrationNumber} · {driver.vehicle.seatCapacity} seats</span><small>VIN {driver.vehicle.vehicleIdentificationNumber}</small></div><StatusBadge status={driver.vehicle.reviewStatus} /></div> : <div className="empty-inline">No vehicle registered.</div>}</section>
          {error && <div className="inline-alert"><Icon name="alert" />{error}</div>}
        </div>
        <footer className="drawer-actions"><button className="button button-secondary" disabled={!isPending || busy} onClick={() => void decideDriver(false)}>Reject application</button><button className="button button-primary" disabled={!canApprove || busy} onClick={() => void decideDriver(true)}><Icon name="check" />Approve driver</button></footer>
      </section>
      {selectedDocument && <section className="document-modal" role="dialog" aria-modal="true" aria-label="Document review">
        <header><div><p className="eyebrow">Protected document</p><h2>{documentLabels[selectedDocument.documentType]}</h2><span>{selectedDocument.originalFileName}</span></div><button className="icon-button" onClick={() => setSelectedDocument(null)}><Icon name="close" /></button></header>
        <div className="document-preview">{previewUrl ? selectedDocument.contentType.startsWith('image/') ? <img src={previewUrl} alt={documentLabels[selectedDocument.documentType]} /> : <iframe title={documentLabels[selectedDocument.documentType]} src={previewUrl} /> : <div className="loading-state">Loading protected document…</div>}</div>
        {selectedDocument.reviewStatus === 'PendingReview' && isPending && <div className="document-decision"><label>Feedback if invalid<textarea value={reason} onChange={(event) => setReason(event.target.value)} placeholder="Be specific so the driver can correct it…" /></label><div className="preset-list">{rejectionPresets.map((preset) => <button key={preset} onClick={() => setReason(preset)}>{preset}</button>)}</div>{error && <div className="inline-alert"><Icon name="alert" />{error}</div>}<div className="decision-actions"><button className="button button-danger" disabled={busy} onClick={() => void decideDocument(false)}>Reject document</button><button className="button button-primary" disabled={busy} onClick={() => void decideDocument(true)}><Icon name="check" />Document is valid</button></div></div>}
      </section>}
    </div>
  )
}

function App() {
  const [session, setSessionState] = useState<AdminSession | null>(() => readSession())
  const [view, setView] = useState<View>('overview')
  const [data, setData] = useState<DashboardData | null>(null)
  const [selectedDriver, setSelectedDriver] = useState<DriverApplication | null>(null)
  const [driverFilter, setDriverFilter] = useState<'All' | DriverStatus>('PendingReview')
  const [search, setSearch] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)
  const [live, setLive] = useState(false)
  const refreshTimer = useRef<number | null>(null)
  const updateSession = useCallback((next: AdminSession | null) => { writeSession(next); setSessionState(next) }, [])
  const api = useMemo(() => new AdminApi(() => readSession(), updateSession), [updateSession])

  const refresh = useCallback(async (silent = false) => {
    if (!readSession()) return
    if (!silent) setLoading(true)
    setError('')
    try { setData(await api.loadDashboard()) }
    catch (reason) { if (reason instanceof ApiError && reason.status === 401) updateSession(null); else setError(reason instanceof Error ? reason.message : 'Dashboard data could not be loaded.') }
    finally { if (!silent) setLoading(false) }
  }, [api, updateSession])

  useEffect(() => { if (session) void refresh() }, [session, refresh])
  useEffect(() => {
    if (!session) return
    const connection = new HubConnectionBuilder().withUrl(`${appConfig.apiBaseUrl}/hubs/operations`, { accessTokenFactory: () => readSession()?.accessToken ?? '' }).withAutomaticReconnect().configureLogging(LogLevel.Warning).build()
    const scheduleRefresh = () => { if (refreshTimer.current) window.clearTimeout(refreshTimer.current); refreshTimer.current = window.setTimeout(() => void refresh(true), 350) }
    for (const eventName of ['TripUpdated', 'DriverAvailabilityUpdated', 'PaymentUpdated', 'DisputeUpdated', 'DriverReviewUpdated', 'AdminOperationsChanged']) connection.on(eventName, scheduleRefresh)
    connection.onreconnecting(() => setLive(false)); connection.onreconnected(() => { setLive(true); scheduleRefresh() }); connection.onclose(() => setLive(false))
    void connection.start().then(() => setLive(true)).catch(() => setLive(false))
    return () => { if (refreshTimer.current) window.clearTimeout(refreshTimer.current); void connection.stop() }
  }, [session, refresh])

  if (!session) return <Login api={api} onLogin={updateSession} />
  const pendingDrivers = data?.drivers.items.filter((driver) => driver.profile.onboardingStatus === 'PendingReview') ?? []
  const activeTrips = data?.trips.items.filter((trip) => !['Completed', 'Cancelled'].includes(trip.status)) ?? []
  const visibleDrivers = (data?.drivers.items ?? []).filter((driver) => (driverFilter === 'All' || driver.profile.onboardingStatus === driverFilter) && `${driver.profile.firstName} ${driver.profile.lastName} ${driver.profile.userId}`.toLowerCase().includes(search.toLowerCase()))

  return (
    <div className="app-shell">
      <aside className="sidebar"><div className="brand-lockup"><span className="brand-symbol">R</span><div><span className="brand-name">RYDO</span><span className="brand-surface">Operations</span></div></div><nav className="primary-navigation" aria-label="Primary navigation">{navItems.map((item) => <button key={item.id} className={view === item.id ? 'navigation-item navigation-item-active' : 'navigation-item'} onClick={() => setView(item.id)}><Icon name={item.icon} /><span>{item.label}</span>{item.id === 'drivers' && pendingDrivers.length > 0 && <b>{pendingDrivers.length}</b>}</button>)}</nav><div className="sidebar-footer"><div className="connection-state"><span className={live ? 'online' : ''} />{live ? 'Live updates connected' : 'REST connected'}</div><button className="navigation-item" onClick={() => void api.logout()}><Icon name="logout" />Sign out</button></div></aside>
      <main className="main-content">
        <header className="topbar"><div><p className="eyebrow">{appConfig.environment} workspace</p><h1>{navItems.find((item) => item.id === view)?.label}</h1></div><div className="topbar-actions"><button className="icon-button" onClick={() => void refresh()} aria-label="Refresh"><Icon name="refresh" /></button><div className="admin-avatar">AD</div></div></header>
        {error && <div className="inline-alert page-alert"><Icon name="alert" />{error}<button onClick={() => void refresh()}>Retry</button></div>}
        {loading && !data ? <div className="loading-page"><span /><strong>Loading operations…</strong></div> : data && <>
          {view === 'overview' && <><section className="metric-grid"><Metric label="Active trips" value={data.overview.activeTripCount} icon="route" detail="Currently moving" /><Metric label="Drivers online" value={data.overview.onlineDriverCount} icon="car" detail="Location connected" /><Metric label="Pending reviews" value={data.overview.pendingDriverCount} icon="drivers" detail="Needs your decision" /><Metric label="Open disputes" value={data.overview.openDisputeCount} icon="disputes" detail="Customer support" /></section><div className="dashboard-grid"><section className="panel map-panel"><PanelHeader title="Live network" subtitle={`${data.liveDrivers.length} online · ${activeTrips.length} active trips`} action={() => setView('operations')} /><LiveMap drivers={data.liveDrivers} trips={activeTrips} /></section><section className="panel"><PanelHeader title="Driver review queue" subtitle="Oldest applications first" action={() => setView('drivers')} /><div className="compact-list">{pendingDrivers.slice(0, 5).map((driver) => <button key={driver.profile.userId} onClick={() => setSelectedDriver(driver)}><Avatar name={`${driver.profile.firstName} ${driver.profile.lastName}`} /><span><strong>{driver.profile.firstName} {driver.profile.lastName}</strong><small>{driver.documents.length}/3 documents · {driver.vehicle ? 'Vehicle added' : 'No vehicle'}</small></span><StatusBadge status="PendingReview" /></button>)}{pendingDrivers.length === 0 && <Empty title="Review queue is clear" text="New driver applications will appear here." />}</div></section></div></>}
          {view === 'drivers' && <section className="panel data-panel"><div className="toolbar"><div className="search-box"><Icon name="search" /><input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search name or application ID" /></div><div className="filter-tabs">{(['PendingReview', 'Approved', 'Rejected', 'All'] as const).map((status) => <button className={driverFilter === status ? 'active' : ''} key={status} onClick={() => setDriverFilter(status)}>{status.replace(/([a-z])([A-Z])/g, '$1 $2')}</button>)}</div></div><div className="application-grid">{visibleDrivers.map((driver) => <button className="application-card" key={driver.profile.userId} onClick={() => setSelectedDriver(driver)}><div className="application-top"><Avatar name={`${driver.profile.firstName} ${driver.profile.lastName}`} /><StatusBadge status={driver.profile.onboardingStatus} /></div><h3>{driver.profile.firstName} {driver.profile.lastName}</h3><span className="application-id">Application {shortId(driver.profile.userId)}</span><div className="application-checks"><span className={driver.documents.length === 3 ? 'ready' : ''}><Icon name="file" />{driver.documents.length}/3 documents</span><span className={driver.vehicle ? 'ready' : ''}><Icon name="car" />{driver.vehicle ? driver.vehicle.registrationNumber : 'No vehicle'}</span></div><footer>Updated {formatDate(driver.profile.updatedAt)}<Icon name="eye" /></footer></button>)}{visibleDrivers.length === 0 && <Empty title="No applications found" text="Try another status or search term." />}</div></section>}
          {view === 'operations' && <div className="operations-layout"><section className="panel map-panel large"><PanelHeader title="Live operations map" subtitle="Driver positions and active trip endpoints" /><LiveMap drivers={data.liveDrivers} trips={activeTrips} /></section><section className="panel side-list"><PanelHeader title="Online drivers" subtitle={`${data.liveDrivers.length} connected`} /><div className="compact-list">{data.liveDrivers.map((driver) => <div className="static-row" key={driver.driverUserId}><Avatar name={driver.displayName} online /><span><strong>{driver.displayName}</strong><small>{driver.latitude.toFixed(5)}, {driver.longitude.toFixed(5)} · {formatDate(driver.locationUpdatedAt)}</small></span></div>)}{!data.liveDrivers.length && <Empty title="No drivers online" text="Online drivers will appear live." />}</div><PanelHeader title="Active trips" subtitle={`${activeTrips.length} in progress`} /><div className="compact-list">{activeTrips.map((trip) => <div className="static-row" key={trip.id}><span className="soft-icon"><Icon name="route" /></span><span><strong>{trip.pickupAddress} → {trip.destinationAddress}</strong><small>{trip.rideCategory ?? 'Ride'} · {money(trip.estimatedFareAmount, trip.fareCurrency ?? 'ZAR')}</small></span><StatusBadge status={trip.status} /></div>)}</div></section></div>}
          {view === 'users' && <DataTable headers={['User', 'Role', 'Phone', 'Joined', 'State']} rows={data.users.items.map((user) => [<strong key="name">{user.displayName ?? 'Profile incomplete'}<small>{shortId(user.id)}</small></strong>, <StatusBadge key="role" status={user.role} />, user.phoneNumber, formatDate(user.createdAt), <StatusBadge key="state" status={user.isActive ? 'Active' : 'Disabled'} />])} />}
          {view === 'payments' && <DataTable headers={['Payment', 'Method', 'Amount', 'Status', 'Updated', 'Issue']} rows={data.payments.items.map((payment) => [<strong key="id">{shortId(payment.id)}<small>Trip {shortId(payment.tripId)}</small></strong>, payment.method, money(payment.amount, payment.currency), <StatusBadge key="status" status={payment.status} />, formatDate(payment.updatedAt), payment.failureReason ?? '—'])} />}
          {view === 'disputes' && <section className="panel data-panel"><div className="dispute-list">{data.disputes.items.map((item) => <DisputeCard key={item.dispute.id} item={item} api={api} onChanged={() => void refresh(true)} />)}{!data.disputes.items.length && <Empty title="No disputes" text="Customer and driver disputes will appear here." />}</div></section>}
          {view === 'audit' && <DataTable headers={['Time', 'Action', 'Entity', 'Admin', 'Details']} rows={data.audit.items.map((entry) => [formatDate(entry.createdAt), <strong key="action">{entry.action}</strong>, `${entry.entityType} · ${shortId(entry.entityId)}`, shortId(entry.adminUserId), entry.details])} />}
        </>}
      </main>
      {selectedDriver && <DriverReview driver={selectedDriver} api={api} onClose={() => setSelectedDriver(null)} onChanged={() => { setSelectedDriver(null); void refresh(true) }} />}
    </div>
  )
}

function Metric({ label, value, icon, detail }: { label: string; value: number; icon: IconName; detail: string }) { return <article className="metric-card"><span className="metric-icon"><Icon name={icon} /></span><p>{label}</p><strong>{value}</strong><small>{detail}</small></article> }
function PanelHeader({ title, subtitle, action }: { title: string; subtitle: string; action?: () => void }) { return <header className="panel-header"><div><h2>{title}</h2><p>{subtitle}</p></div>{action && <button onClick={action}>View all →</button>}</header> }
function Avatar({ name, online = false }: { name: string; online?: boolean }) { const initials = name.split(' ').map((part) => part[0]).slice(0, 2).join('').toUpperCase(); return <span className="avatar">{initials}{online && <i />}</span> }
function Empty({ title, text }: { title: string; text: string }) { return <div className="empty-state"><span className="soft-icon"><Icon name="check" /></span><strong>{title}</strong><p>{text}</p></div> }
function DataTable({ headers, rows }: { headers: string[]; rows: React.ReactNode[][] }) { return <section className="panel table-panel"><div className="table-scroll"><table><thead><tr>{headers.map((header) => <th key={header}>{header}</th>)}</tr></thead><tbody>{rows.map((row, index) => <tr key={index}>{row.map((cell, cellIndex) => <td key={cellIndex}>{cell}</td>)}</tr>)}</tbody></table>{rows.length === 0 && <Empty title="Nothing to show" text="Records will appear here as the platform is used." />}</div></section> }
function DisputeCard({ item, api, onChanged }: { item: Dispute; api: AdminApi; onChanged(): void }) { const [busy, setBusy] = useState(false); async function review(status: 'UnderReview' | 'Resolved' | 'Rejected') { const resolution = status === 'UnderReview' ? '' : window.prompt(status === 'Resolved' ? 'Enter the resolution shared with the customer:' : 'Explain why this dispute is rejected:') ?? ''; if (status !== 'UnderReview' && !resolution.trim()) return; setBusy(true); try { await api.reviewDispute(item.dispute.id, status, resolution); onChanged() } finally { setBusy(false) } } return <article className="dispute-card"><header><div><span>{item.dispute.category}</span><h3>{item.dispute.subject}</h3></div><StatusBadge status={item.dispute.status} /></header><p>{item.dispute.description}</p><div className="dispute-meta">Trip {shortId(item.dispute.tripId)} · Opened {formatDate(item.dispute.createdAt)} · {item.dispute.messages.length} messages</div>{item.dispute.resolution && <blockquote>{item.dispute.resolution}</blockquote>}{['Open', 'UnderReview'].includes(item.dispute.status) && <footer><button disabled={busy || item.dispute.status === 'UnderReview'} onClick={() => void review('UnderReview')} className="button button-secondary">Mark under review</button><button disabled={busy} onClick={() => void review('Rejected')} className="button button-danger">Reject</button><button disabled={busy} onClick={() => void review('Resolved')} className="button button-primary">Resolve</button></footer>}</article> }

export default App
