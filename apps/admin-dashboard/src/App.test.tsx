import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import App from './App'

describe('Admin dashboard foundation', () => {
  it('renders the operations shell and planned modules', () => {
    render(<App />)

    expect(screen.getByRole('heading', { name: 'Operations overview' })).toBeInTheDocument()
    expect(screen.getByRole('navigation', { name: 'Primary navigation' })).toBeInTheDocument()
    expect(screen.getByText('Local environment')).toBeInTheDocument()
    expect(screen.getByText('Users and drivers')).toBeInTheDocument()
    expect(screen.getByText('Trips and matching')).toBeInTheDocument()
    expect(screen.getByText('Payments and disputes')).toBeInTheDocument()
  })
})
