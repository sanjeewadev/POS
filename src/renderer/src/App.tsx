// src/renderer/src/App.tsx
import { useState } from 'react'
import { useAuth } from './store/AuthContext'

// --- Shell & Layout ---
import AppLayout from './layout/AppLayout/AppLayout'
import LoginView from './pages/Login/LoginView'

// --- Workspaces ---
import POSWorkspace from './pages/POS/POSWorkspace'
import ReturnsCenter from './pages/Returns/ReturnsCenter'
import InventoryWorkspace from './pages/Inventory/InventoryWorkspace'
import ReportsWorkspace from './pages/Reports/ReportsWorkspace'
import TodaySales from './pages/TodaySales/TodaySales'
import SettingsWorkspace from './pages/Settings/SettingsWorkspace'

function App() {
  // 1. ALL HOOKS MUST BE AT THE TOP
  const { currentUser } = useAuth()
  const [currentMode, setCurrentMode] = useState('POS')

  // This function handles the quick access button click
  const handleOpenTodaysSales = () => {
    setCurrentMode('TodaySales')
  }

  // 2. HELPER FUNCTIONS
  const renderWorkspace = () => {
    switch (currentMode) {
      case 'POS':
        return <POSWorkspace />
      case 'Returns':
        return <ReturnsCenter />
      case 'Inventory':
        return <InventoryWorkspace />
      case 'Reports':
        return <ReportsWorkspace />
      case 'TodaySales': // 🚀 NEW: Add this case for full screen access
        return <TodaySales />
      case 'Settings':
        // 🚀 RBAC: Strict protection for the Settings module
        if (currentUser?.Role !== 0 && currentUser?.Role !== 1) {
          return (
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                height: '100%',
                backgroundColor: 'var(--bg-main)'
              }}
            >
              <div
                style={{
                  padding: '40px',
                  textAlign: 'center',
                  backgroundColor: '#fef2f2',
                  border: '2px solid #fecaca',
                  borderRadius: 'var(--radius-lg)',
                  boxShadow: 'var(--shadow-panel)'
                }}
              >
                <div style={{ fontSize: '48px', marginBottom: '10px' }}>⛔</div>
                <h2
                  style={{
                    margin: 0,
                    color: 'var(--action-danger)',
                    fontSize: '24px',
                    fontWeight: 900
                  }}
                >
                  ACCESS DENIED
                </h2>
                <p style={{ color: '#991b1b', fontWeight: 600, marginTop: '10px' }}>
                  You do not have permission to view System Settings.
                </p>
              </div>
            </div>
          )
        }
        return <SettingsWorkspace />
      default:
        return <POSWorkspace />
    }
  }

  // 3. ONE SINGLE RETURN STATEMENT AT THE BOTTOM
  return (
    <>
      {!currentUser ? (
        <LoginView />
      ) : (
        <AppLayout
          currentMode={currentMode}
          setMode={setCurrentMode}
          onOpenTodaysSales={handleOpenTodaysSales}
        >
          {renderWorkspace()}
        </AppLayout>
      )}
    </>
  )
}

export default App
