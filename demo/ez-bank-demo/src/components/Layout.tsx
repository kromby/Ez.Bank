import { Link, useLocation } from 'react-router-dom'
import type { UserRole } from '../types'

interface LayoutProps {
  children: React.ReactNode
  role: UserRole
  onRoleChange: (role: UserRole) => void
}

const creditorNav = [
  { to: '/', label: 'Dashboard' },
  { to: '/claims', label: 'Claims' },
  { to: '/claims/new', label: 'Create Claim' },
]

const debtorNav = [
  { to: '/', label: 'Dashboard' },
  { to: '/claims', label: 'My Claims' },
]

export function Layout({ children, role, onRoleChange }: LayoutProps) {
  const location = useLocation()
  const nav = role === 'Creditor' ? creditorNav : debtorNav

  return (
    <div className="min-h-screen">
      <nav className="bg-white border-b border-gray-200">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex justify-between h-16">
            <div className="flex">
              <div className="flex-shrink-0 flex items-center">
                <Link to="/" className="text-xl font-bold text-indigo-600">
                  Ez.Bank Claims
                </Link>
              </div>
              <div className="ml-10 flex items-center space-x-4">
                {nav.map((item) => (
                  <Link
                    key={item.to}
                    to={item.to}
                    className={`px-3 py-2 rounded-md text-sm font-medium ${
                      location.pathname === item.to
                        ? 'bg-indigo-100 text-indigo-700'
                        : 'text-gray-500 hover:text-gray-700 hover:bg-gray-100'
                    }`}
                  >
                    {item.label}
                  </Link>
                ))}
              </div>
            </div>
            <div className="flex items-center space-x-3">
              <span className="text-sm text-gray-500">Role:</span>
              <select
                value={role}
                onChange={(e) => onRoleChange(e.target.value as UserRole)}
                className="rounded-md border-gray-300 text-sm py-1 px-2 border focus:outline-none focus:ring-2 focus:ring-indigo-500"
              >
                <option value="Creditor">Creditor</option>
                <option value="Debtor">Debtor</option>
              </select>
            </div>
          </div>
        </div>
      </nav>
      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        {children}
      </main>
    </div>
  )
}
