import './App.css'

const navigationItems = ['Overview', 'Drivers', 'Trips', 'Payments', 'Disputes']

const metrics = [
  { label: 'Active trips', value: '0', detail: 'Live operations will connect later' },
  { label: 'Drivers online', value: '0', detail: 'Availability service not connected' },
  { label: 'Pending reviews', value: '0', detail: 'Driver verification comes next' },
  { label: 'Payment alerts', value: '0', detail: 'Payment monitoring comes later' },
]

const modules = [
  {
    title: 'Users and drivers',
    description: 'Profiles, onboarding, documents, and verification.',
  },
  {
    title: 'Trips and matching',
    description: 'Ride lifecycle, assignments, exceptions, and live activity.',
  },
  {
    title: 'Payments and disputes',
    description: 'Payment visibility, event history, and support workflows.',
  },
]

function App() {
  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand-lockup" aria-label="RYDO Operations">
          <span className="brand-name">RYDO</span>
          <span className="brand-surface">Operations</span>
        </div>

        <nav className="primary-navigation" aria-label="Primary navigation">
          <p className="navigation-label">Workspace</p>
          {navigationItems.map((item, index) => (
            <button
              className={index === 0 ? 'navigation-item navigation-item-active' : 'navigation-item'}
              key={item}
              type="button"
            >
              <span className="navigation-dot" aria-hidden="true" />
              {item}
            </button>
          ))}
        </nav>

        <div className="sidebar-status">
          <span className="status-indicator" aria-hidden="true" />
          <div>
            <strong>Local environment</strong>
            <span>Backend not connected</span>
          </div>
        </div>
      </aside>

      <main className="main-content">
        <header className="page-header">
          <div>
            <p className="eyebrow">Admin dashboard</p>
            <h1>Operations overview</h1>
            <p className="page-description">
              A clear control surface for RYDO users, drivers, trips, and payments.
            </p>
          </div>
          <div className="environment-badge">
            <span aria-hidden="true" />
            Foundation
          </div>
        </header>

        <section className="metric-grid" aria-label="Operational metrics">
          {metrics.map((metric) => (
            <article className="metric-card" key={metric.label}>
              <p>{metric.label}</p>
              <strong>{metric.value}</strong>
              <span>{metric.detail}</span>
            </article>
          ))}
        </section>

        <section className="readiness-panel" aria-labelledby="readiness-title">
          <div className="section-heading">
            <div>
              <p className="eyebrow">Platform readiness</p>
              <h2 id="readiness-title">Operations modules</h2>
            </div>
            <span className="readiness-state">Foundation ready</span>
          </div>

          <div className="module-list">
            {modules.map((module, index) => (
              <article className="module-row" key={module.title}>
                <span className="module-number" aria-hidden="true">
                  {String(index + 1).padStart(2, '0')}
                </span>
                <div>
                  <h3>{module.title}</h3>
                  <p>{module.description}</p>
                </div>
                <span className="module-status">Planned</span>
              </article>
            ))}
          </div>
        </section>
      </main>
    </div>
  )
}

export default App
